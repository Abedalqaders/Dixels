using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Reservations;
using Dixels.Localization;
using Dixels.Permissions;
using Dixels.SpaceManagement.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Users;

namespace Dixels.SpaceManagement;

// Only signed-in users at class level: reads admit several permissions (see
// DixelsPermissions.Readers), and ABP adds a class-level [Authorize(...)] to every method's own.
// So every method states what it needs — a new one must too.
[Authorize]
public class FloorsAppService : DixelsAppService, IFloorsAppService
{
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly ConstraintResolver _constraintResolver;
    private readonly SpaceHierarchyManager _spaceHierarchyManager;
    private readonly ISpaceHierarchyBulkRepository _hierarchyBulk;
    private readonly IDataFilter _dataFilter;
    private readonly ReservationImpactPreview _impactPreview;
    private readonly ILocalEventBus _localEventBus;
    private readonly LocalizedNameValidator _nameValidator;
    private readonly LocalizedNameReader _nameReader;

    public FloorsAppService(
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        IRepository<Space, Guid> spaceRepository,
        ConstraintResolver constraintResolver,
        SpaceHierarchyManager spaceHierarchyManager,
        ISpaceHierarchyBulkRepository hierarchyBulk,
        IDataFilter dataFilter,
        ReservationImpactPreview impactPreview,
        ILocalEventBus localEventBus,
        LocalizedNameValidator nameValidator,
        LocalizedNameReader nameReader)
    {
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _spaceRepository = spaceRepository;
        _constraintResolver = constraintResolver;
        _spaceHierarchyManager = spaceHierarchyManager;
        _hierarchyBulk = hierarchyBulk;
        _dataFilter = dataFilter;
        _impactPreview = impactPreview;
        _localEventBus = localEventBus;
        _nameValidator = nameValidator;
        _nameReader = nameReader;
    }

    public async Task<FloorDto> GetAsync(Guid id)
    {
        await CheckAnyPermissionAsync(DixelsPermissions.Readers.Floors);
        var floor = await _floorRepository.GetAsync(id);
        return await MapToDtoAsync(floor);
    }

    public async Task<PagedResultDto<FloorDto>> GetListAsync(GetFloorsInput input)
    {
        await CheckAnyPermissionAsync(DixelsPermissions.Readers.Floors);

        // Floor has no EF navigation to Building (separate aggregate roots, FK-only), so
        // BuildingName is resolved with an explicit join — one SQL query, not one lookup per
        // row. Joining unconditionally (even when BuildingId scopes to one building) keeps a
        // single code path instead of branching on whether the caller is the standalone
        // Floors page or the drill-down Floors-of-a-building page.
        //
        // Names: search matches a name in any language, ignoring case; rows are sorted by the
        // names the reader sees (theirs, else the default language's), worked out in the query.
        var (shown, fallback) = await _nameReader.GetLanguagesAsync();
        var term = input.Filter.IsNullOrWhiteSpace() ? null : NameTranslation.Normalize(input.Filter!);

        async Task<PagedResultDto<FloorDto>> QueryAsync()
        {
            var floorsQueryable = await _floorRepository.WithDetailsAsync();
            var buildingsQueryable = await _buildingRepository.GetQueryableAsync();

            var joined =
                from f in floorsQueryable
                join b in buildingsQueryable on f.BuildingId equals b.Id
                where input.BuildingId == null || f.BuildingId == input.BuildingId
                where term == null
                    || f.Translations.Any(t => t.NormalizedName.Contains(term))
                    || (!input.FloorNameOnly && b.Translations.Any(t => t.NormalizedName.Contains(term)))
                select new
                {
                    Floor = f,
                    BuildingName = b.Translations.Where(t => t.Language == shown).Select(t => t.Name).FirstOrDefault()
                        ?? b.Translations.Where(t => t.Language == fallback).Select(t => t.Name).FirstOrDefault(),
                    FloorName = f.Translations.Where(t => t.Language == shown).Select(t => t.Name).FirstOrDefault()
                        ?? f.Translations.Where(t => t.Language == fallback).Select(t => t.Name).FirstOrDefault(),
                };

            var totalCount = await AsyncExecuter.CountAsync(joined);
            var page = await AsyncExecuter.ToListAsync(
                joined.OrderBy(x => x.BuildingName).ThenBy(x => x.FloorName).Skip(input.SkipCount).Take(input.MaxResultCount));

            var items = new List<FloorDto>();
            foreach (var x in page)
            {
                var dto = await MapToDtoAsync(x.Floor);
                dto.BuildingName = x.BuildingName;
                items.Add(dto);
            }

            return new PagedResultDto<FloorDto>(totalCount, items);
        }

        if (input.IncludeDeleted)
        {
            using (_dataFilter.Disable<ISoftDelete>())
            {
                return await QueryAsync();
            }
        }

        return await QueryAsync();
    }

    [Authorize(DixelsPermissions.Floors.Create)]
    public async Task<FloorDto> CreateAsync(CreateFloorDto input)
    {
        await EnsureCanManageBuildingAsync(input.BuildingId);

        // 404 for an unknown or deleted building, instead of a foreign-key failure (500).
        await _buildingRepository.EnsureExistsAsync(input.BuildingId);

        var names = await _nameValidator.NormalizeAsync(input.Names.ToNames());
        var floor = new Floor(GuidGenerator.Create(), input.BuildingId, names[0].Language, names[0].Name, input.FloorNumber);
        floor.SetNames(names);
        await _floorRepository.InsertAsync(floor);

        return await MapToDtoAsync(floor);
    }

    [Authorize(DixelsPermissions.Floors.Edit)]
    public async Task<FloorDto> UpdateAsync(Guid id, UpdateFloorDto input)
    {
        var floor = await _floorRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(floor.BuildingId);

        floor.SetNames(await _nameValidator.NormalizeAsync(input.Names.ToNames()));
        floor.SetFloorNumber(input.FloorNumber);

        await _floorRepository.UpdateAsync(floor);

        return await MapToDtoAsync(floor);
    }

    [Authorize(DixelsPermissions.Floors.Edit)]
    public async Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, UpdateFloorConstraintsDto input)
    {
        var floor = await _floorRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(floor.BuildingId);

        var building = await _buildingRepository.GetAsync(floor.BuildingId);
        floor.ConcurrencyStamp = input.ConcurrencyStamp;

        var proposedDays = ConstraintDtoConversions.ToOperatingDaysOrNull(input.Days);
        var proposedHours = ConstraintDtoConversions.ToOperatingWindowOrNull(input.Hours);

        // The floor's rooms are read once: for the warnings, the event and (when cancelling) the check.
        var spaces = await _spaceRepository.GetListAsync(s => s.FloorId == id, includeDetails: true);

        // Computed before saving — the tightening itself is never blocked, this is purely
        // informational for the admin to go fix the named Spaces afterward.
        var warnings = await FindNarrowingConflictsAsync(spaces, proposedDays, proposedHours);

        // When the admin chose to cancel what no longer fits: worked out on an unsaved copy,
        // before the real floor changes, and carried by the event so no listener checks it all
        // again. Kept bookings need no check.
        var affected = input.CancelAffectedBookings
            ? await _impactPreview.AffectedAsync(ProposedChange(building, floor, input, spaces))
            : null;

        // Validated against the Building's raw values directly — Building has no parent of
        // its own, so its own fields already are the "resolved" value.
        floor.SetOwnOperatingDays(proposedDays, building.Days);
        floor.SetOwnOperatingHours(proposedHours, building.Hours);
        floor.SetOwnMaxDuration(input.MaxDurationMinutes);

        await _floorRepository.UpdateAsync(floor);
        await CurrentUnitOfWork!.SaveChangesAsync();
        // What the rooms hold is released (or kept, as the admin chose) by its own module.
        await _localEventBus.PublishAsync(new SpaceRulesChangedEvent(
            building.Id, spaces.Select(s => s.Id).ToList(), input.CancelAffectedBookings, CurrentUser.GetId(), affected));

        return new ConstraintsSaveResultDto
        {
            ConcurrencyStamp = floor.ConcurrencyStamp,
            Warnings = warnings,
            CancelledBookings = affected?.Count ?? 0,
        };
    }

    [Authorize(DixelsPermissions.Floors.Edit)]
    public async Task<ReservationImpactDto> GetConstraintsImpactAsync(Guid id, UpdateFloorConstraintsDto input, int skip = 0)
    {
        var floor = await _floorRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(floor.BuildingId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);
        var spaces = await _spaceRepository.GetListAsync(s => s.FloorId == floor.Id);
        return await _impactPreview.NoLongerFittingAsync(ProposedChange(building, floor, input, spaces), skip);
    }

    [Authorize(DixelsPermissions.Floors.Delete)]
    public async Task<ReservationImpactDto> GetDeleteImpactAsync(Guid id, int skip = 0)
    {
        var floor = await _floorRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(floor.BuildingId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);
        return await _impactPreview.UpcomingAsync(building, new RoomScope(building.Id, floor.Id), L["Dixels:Bookings:CancelReason:FloorRemoved"], skip);
    }

    // The proposed floor is a fresh, untracked copy — checking it can never save anything.
    /// <summary>The floor's rules as the admin proposes them, on an unsaved copy, for every room on it.</summary>
    private RoomRulesChange ProposedChange(Building building, Floor floor, UpdateFloorConstraintsDto input, IReadOnlyList<Space> spaces)
    {
        // Its name plays no part in the check — any one of them will do.
        var name = floor.Translations.First();
        var proposed = new Floor(floor.Id, floor.BuildingId, name.Language, name.Name, floor.FloorNumber);
        proposed.SetOwnOperatingDays(ConstraintDtoConversions.ToOperatingDaysOrNull(input.Days), building.Days);
        proposed.SetOwnOperatingHours(ConstraintDtoConversions.ToOperatingWindowOrNull(input.Hours), building.Hours);
        proposed.SetOwnMaxDuration(input.MaxDurationMinutes);

        return new RoomRulesChange(
            building, spaces.Select(s => (s, floor)).ToList(), (space, _) => _constraintResolver.Resolve(building, proposed, space));
    }

    [Authorize(DixelsPermissions.Floors.Default)]
    public async Task<ResolvedConstraintsDto> GetResolvedConstraintsAsync(Guid id)
    {
        var floor = await _floorRepository.GetAsync(id);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);

        var resolved = _constraintResolver.Resolve(building, floor);

        return new ResolvedConstraintsDto
        {
            Timezone = resolved.Timezone,
            Days = new FieldValueDto<int[]>
            {
                Value = ConstraintDtoConversions.ToDayArray(resolved.Days.Value),
                Source = resolved.Days.Source.ToString(),
            },
            Hours = new FieldValueDto<OperatingWindowDto>
            {
                Value = ConstraintDtoConversions.ToWindowDto(resolved.Hours.Value),
                Source = resolved.Hours.Source.ToString(),
            },
            MaxDurationMinutes = new FieldValueDto<int>
            {
                Value = resolved.MaxDurationMinutes.Value,
                Source = resolved.MaxDurationMinutes.Source.ToString(),
            },
            MaxHorizonDays = resolved.MaxHorizonDays,
            MinLeadMinutes = resolved.MinLeadMinutes,
            MinAttendees = resolved.MinAttendees,
            Capacity = resolved.Capacity,
            BuildingId = building.Id,
            BuildingName = await _nameReader.ShownAsync(building),
        };
    }

    [Authorize(DixelsPermissions.Floors.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var floor = await _floorRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(floor.BuildingId);

        // One UPDATE per table, however many rooms: the floor and its rooms under one batch id.
        var spaceIds = await _hierarchyBulk.SoftDeleteFloorAsync(id, GuidGenerator.Create(), Clock.Now, CurrentUser.Id);

        // What its rooms held is released by its own module (in the background: the delete
        // doesn't wait for it).
        await _localEventBus.PublishAsync(new FloorDeletedEvent(id, spaceIds, CurrentUser.GetId()));
    }

    [Authorize(DixelsPermissions.Floors.Edit)]
    public async Task RestoreAsync(Guid id)
    {
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var floor = await _floorRepository.GetAsync(id);
            await EnsureCanManageBuildingAsync(floor.BuildingId);

            // Restoring a floor under a building that is still deleted would leave it reachable
            // by id but invisible in every list — restore the building first.
            var building = await _buildingRepository.GetAsync(floor.BuildingId);
            if (building.IsDeleted)
            {
                throw new BusinessException(DixelsDomainErrorCodes.ParentIsDeleted).WithData("parent", "building");
            }

            var batchId = floor.DeletionBatchId;

            if (batchId is null)
            {
                floor.IsDeleted = false;
                await _floorRepository.UpdateAsync(floor);
                await CurrentUnitOfWork!.SaveChangesAsync();
                return;
            }

            var candidateSpaces = await _spaceRepository.GetListAsync(s => s.FloorId == id);
            var restoredSpaces = _spaceHierarchyManager.SelectAndClearForRestore(batchId.Value, candidateSpaces);

            floor.ClearDeletionBatch();
            floor.IsDeleted = false;
            await _floorRepository.UpdateAsync(floor);

            foreach (var space in restoredSpaces)
            {
                space.IsDeleted = false;
                await _spaceRepository.UpdateAsync(space);
            }

            await CurrentUnitOfWork!.SaveChangesAsync();
        }
    }

    /// <summary>The rooms (read with their names) whose own days or hours the proposed floor rules would cut into.</summary>
    private async Task<List<string>> FindNarrowingConflictsAsync(IReadOnlyList<Space> floorSpaces, OperatingDays? proposedDays, OperatingWindow? proposedHours)
    {
        var spaces = floorSpaces.Where(s => s.Days is not null || s.Hours is not null).ToList();
        var names = await _nameReader.ShownAsync<Space, SpaceTranslation>(spaces);
        var candidates = spaces.Select(s => new NarrowingCandidate(names[s.Id], s.Days, s.Hours)).ToList();

        return _constraintResolver.FindNarrowingConflicts(candidates, proposedDays, proposedHours).ToList();
    }

    private static Task EnsureCanManageBuildingAsync(Guid buildingId)
    {
        // Same extensibility hook as BuildingsAppService's: where a future per-building-admin
        // scoping check plugs in, using buildingId rather than the floor's own id. It must
        // not re-check a flat permission — each method's [Authorize] already names what it
        // needs, and requiring Edit here refused roles that only had Create or Delete.
        _ = buildingId;
        return Task.CompletedTask;
    }

    private async Task<FloorDto> MapToDtoAsync(Floor floor)
    {
        var dto = ObjectMapper.Map<Floor, FloorDto>(floor);
        dto.Name = await _nameReader.ShownAsync(floor);
        dto.Names = floor.Translations.ToNameDtos();
        dto.Days = ConstraintDtoConversions.ToDayArrayOrNull(floor.Days);
        dto.Hours = ConstraintDtoConversions.ToWindowDtoOrNull(floor.Hours);
        dto.HasOverrides = floor.Days is not null || floor.Hours is not null || floor.MaxDurationMinutes is not null;
        dto.IsDeleted = floor.IsDeleted;
        return dto;
    }
}
