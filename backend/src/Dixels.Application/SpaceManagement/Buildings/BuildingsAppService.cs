using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Reservations;
using Dixels.Localization;
using Dixels.Permissions;
using Dixels.Users;
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
public class BuildingsAppService : DixelsAppService, IBuildingsAppService
{
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly ConstraintResolver _constraintResolver;
    private readonly SpaceHierarchyManager _spaceHierarchyManager;
    private readonly ISpaceHierarchyBulkRepository _hierarchyBulk;
    private readonly IDataFilter _dataFilter;
    private readonly ReservationImpactPreview _impactPreview;
    private readonly ILocalEventBus _localEventBus;
    private readonly IUserDirectoryRepository _userDirectory;
    private readonly LocalizedNameValidator _nameValidator;
    private readonly LocalizedNameReader _nameReader;

    public BuildingsAppService(
        IRepository<Building, Guid> buildingRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Space, Guid> spaceRepository,
        ConstraintResolver constraintResolver,
        SpaceHierarchyManager spaceHierarchyManager,
        ISpaceHierarchyBulkRepository hierarchyBulk,
        IDataFilter dataFilter,
        ReservationImpactPreview impactPreview,
        ILocalEventBus localEventBus,
        IUserDirectoryRepository userDirectory,
        LocalizedNameValidator nameValidator,
        LocalizedNameReader nameReader)
    {
        _buildingRepository = buildingRepository;
        _floorRepository = floorRepository;
        _spaceRepository = spaceRepository;
        _constraintResolver = constraintResolver;
        _spaceHierarchyManager = spaceHierarchyManager;
        _hierarchyBulk = hierarchyBulk;
        _dataFilter = dataFilter;
        _impactPreview = impactPreview;
        _localEventBus = localEventBus;
        _userDirectory = userDirectory;
        _nameValidator = nameValidator;
        _nameReader = nameReader;
    }

    public async Task<BuildingDto> GetAsync(Guid id)
    {
        await CheckAnyPermissionAsync(DixelsPermissions.Readers.Buildings);
        var building = await _buildingRepository.GetAsync(id);
        return await MapToDtoAsync(building);
    }

    public async Task<PagedResultDto<BuildingDto>> GetListAsync(GetBuildingsInput input)
    {
        await CheckAnyPermissionAsync(DixelsPermissions.Readers.Buildings);

        // Search matches a name in any language, ignoring case; the page is sorted by the name
        // the reader sees (theirs, else the default language's).
        var (shown, fallback) = await _nameReader.GetLanguagesAsync();

        async Task<PagedResultDto<BuildingDto>> QueryAsync()
        {
            var queryable = await _buildingRepository.WithDetailsAsync();

            if (!input.Filter.IsNullOrWhiteSpace())
            {
                var term = NameTranslation.Normalize(input.Filter!);
                queryable = queryable.Where(b => b.Translations.Any(t => t.NormalizedName.Contains(term)));
            }

            var totalCount = await AsyncExecuter.CountAsync(queryable);
            var buildings = await AsyncExecuter.ToListAsync(
                queryable
                    .OrderBy(b => b.Translations.Where(t => t.Language == shown).Select(t => t.Name).FirstOrDefault()
                        ?? b.Translations.Where(t => t.Language == fallback).Select(t => t.Name).FirstOrDefault())
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount));

            var items = new List<BuildingDto>();
            foreach (var building in buildings)
            {
                items.Add(await MapToDtoAsync(building));
            }

            return new PagedResultDto<BuildingDto>(totalCount, items);
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

    [Authorize(DixelsPermissions.Buildings.Create)]
    public async Task<BuildingDto> CreateAsync(CreateBuildingDto input)
    {
        var names = await _nameValidator.NormalizeAsync(input.Names.ToNames());
        var building = new Building(
            GuidGenerator.Create(),
            names[0].Language,
            names[0].Name,
            input.BuildingNumber,
            input.Timezone,
            ConstraintDtoConversions.ToOperatingDays(input.Days),
            ConstraintDtoConversions.ToOperatingWindow(input.Hours),
            input.MaxDurationMinutes,
            input.MaxHorizonDays,
            input.MinLeadMinutes,
            input.OwnOverlapPolicy,
            input.MaxSeriesHorizonDays);
        building.SetNames(names);
        building.SetAddresses(ToAddressMap(input.Addresses));

        await _buildingRepository.InsertAsync(building);

        return await MapToDtoAsync(building);
    }

    [Authorize(DixelsPermissions.Buildings.Edit)]
    public async Task<BuildingDto> UpdateAsync(Guid id, UpdateBuildingDto input)
    {
        await EnsureCanManageBuildingAsync(id);

        var building = await _buildingRepository.GetAsync(id);

        // Bookings are stored as instants: a new timezone would move every one of them to a
        // different local time (10:00 becomes 07:00) — and possibly outside opening hours.
        if (!string.Equals(input.Timezone, building.Timezone, StringComparison.Ordinal))
        {
            // Only the number is shown, so it's counted — nothing loaded or described.
            var upcoming = await _impactPreview.CountUpcomingAsync((await RoomsAsync(id)).Select(r => r.Space.Id).ToList());
            if (upcoming > 0)
            {
                throw new BusinessException(DixelsDomainErrorCodes.TimezoneChangeWithBookings).WithData("count", upcoming);
            }
        }

        building.SetNames(await _nameValidator.NormalizeAsync(input.Names.ToNames()));
        building.SetAddresses(ToAddressMap(input.Addresses));
        building.SetBuildingNumber(input.BuildingNumber);
        building.SetTimezone(input.Timezone);

        await _buildingRepository.UpdateAsync(building);

        return await MapToDtoAsync(building);
    }

    [Authorize(DixelsPermissions.Buildings.Edit)]
    public async Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, UpdateBuildingConstraintsDto input)
    {
        await EnsureCanManageBuildingAsync(id);

        var building = await _buildingRepository.GetAsync(id);
        building.ConcurrencyStamp = input.ConcurrencyStamp;

        var proposedDays = ConstraintDtoConversions.ToOperatingDays(input.Days);
        var proposedHours = ConstraintDtoConversions.ToOperatingWindow(input.Hours);

        // Computed before saving: the tightening itself is never blocked by this (matching
        // "tightening never retroactively invalidates"), it's purely informational for the
        // admin to go fix the named descendants afterward.
        var warnings = await FindNarrowingConflictsAsync(id, proposedDays, proposedHours);

        // The change on an unsaved copy, before the real building changes: the rooms it
        // reaches and, when the admin chose to cancel what no longer fits, how much that is.
        var change = await ProposedChangeAsync(building, input);
        // Checked once: the event carries what it found, so no listener checks it all again.
        var affected = input.CancelAffectedBookings
            ? ReservationImpactPreview.ToAffected(await _impactPreview.NoLongerFittingAsync(change))
            : null;

        building.SetOperatingDays(proposedDays);
        building.SetOperatingHours(proposedHours);
        building.SetMaxDurationMinutes(input.MaxDurationMinutes);
        building.SetMaxHorizonDays(input.MaxHorizonDays);
        building.SetMinLeadMinutes(input.MinLeadMinutes);
        building.SetOwnOverlapPolicy(input.OwnOverlapPolicy);
        if (input.MaxSeriesHorizonDays is { } seriesHorizon)
        {
            building.SetMaxSeriesHorizonDays(seriesHorizon);
        }

        await _buildingRepository.UpdateAsync(building);

        // The test module disables automatic UoW transactions, so a save isn't guaranteed
        // flushed until the ambient unit of work completes at the very end of this method —
        // but we need the freshly-generated ConcurrencyStamp back *now*, in this same
        // response, so flush explicitly instead of returning whatever's still in memory.
        await CurrentUnitOfWork!.SaveChangesAsync();
        // What the rooms hold is released (or kept, as the admin chose) by its own module.
        await _localEventBus.PublishAsync(new SpaceRulesChangedEvent(
            building.Id, change.Rooms.Select(r => r.Space.Id).ToList(), input.CancelAffectedBookings, CurrentUser.GetId(), affected));

        return new ConstraintsSaveResultDto
        {
            ConcurrencyStamp = building.ConcurrencyStamp,
            Warnings = warnings,
            CancelledBookings = affected?.Count ?? 0,
        };
    }

    [Authorize(DixelsPermissions.Buildings.Edit)]
    public async Task<ReservationImpactDto> GetConstraintsImpactAsync(Guid id, UpdateBuildingConstraintsDto input, int skip = 0)
    {
        await EnsureCanManageBuildingAsync(id);
        var building = await _buildingRepository.GetAsync(id);
        return await _impactPreview.NoLongerFittingAsync(await ProposedChangeAsync(building, input), skip);
    }

    [Authorize(DixelsPermissions.Buildings.Delete)]
    public async Task<ReservationImpactDto> GetDeleteImpactAsync(Guid id, int skip = 0)
    {
        await EnsureCanManageBuildingAsync(id);
        var building = await _buildingRepository.GetAsync(id);
        var impact = await _impactPreview.UpcomingAsync(building, new RoomScope(id), L["Dixels:Bookings:CancelReason:BuildingRemoved"], skip);

        // They keep the assignment (a restore brings everything back), but can't book meanwhile.
        impact.AssignedEmployees = (int)await _userDirectory.GetCountAsync(filter: null, buildingId: id, roleId: null, grantedPermission: null);
        return impact;
    }

    // The proposed building is a fresh, untracked copy — checking it can never save anything.
    /// <summary>The building's rules as the admin proposes them, on an unsaved copy, for every room in it.</summary>
    private async Task<RoomRulesChange> ProposedChangeAsync(Building building, UpdateBuildingConstraintsDto input)
    {
        // Its name plays no part in the check — any one of them will do.
        var name = building.Translations.First();
        var proposed = new Building(
            building.Id,
            name.Language,
            name.Name,
            building.BuildingNumber,
            building.Timezone,
            ConstraintDtoConversions.ToOperatingDays(input.Days),
            ConstraintDtoConversions.ToOperatingWindow(input.Hours),
            input.MaxDurationMinutes,
            input.MaxHorizonDays,
            input.MinLeadMinutes,
            input.OwnOverlapPolicy,
            Math.Max(input.MaxSeriesHorizonDays ?? building.MaxSeriesHorizonDays, input.MaxHorizonDays));

        return new RoomRulesChange(
            building, await RoomsAsync(building.Id), (space, floor) => _constraintResolver.Resolve(proposed, floor, space));
    }

    private async Task<List<(Space Space, Floor Floor)>> RoomsAsync(Guid buildingId)
    {
        var floors = (await _floorRepository.GetListAsync(f => f.BuildingId == buildingId)).ToDictionary(f => f.Id);
        var floorIds = floors.Keys.ToList();
        var spaces = floorIds.Count == 0 ? new List<Space>() : await _spaceRepository.GetListAsync(s => floorIds.Contains(s.FloorId));
        return spaces.Select(s => (s, floors[s.FloorId])).ToList();
    }

    [Authorize(DixelsPermissions.Buildings.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        await EnsureCanManageBuildingAsync(id);
        await _buildingRepository.GetAsync(id); // 404 for a missing or already deleted building

        // One UPDATE per table, however many rooms: the whole building under one batch id.
        var spaceIds = await _hierarchyBulk.SoftDeleteBuildingAsync(id, GuidGenerator.Create(), Clock.Now, CurrentUser.Id);

        // What its rooms held is released by its own module (in the background: the delete
        // doesn't wait for it).
        await _localEventBus.PublishAsync(new BuildingDeletedEvent(id, spaceIds, CurrentUser.GetId()));
    }

    [Authorize(DixelsPermissions.Buildings.Edit)]
    public async Task RestoreAsync(Guid id)
    {
        await EnsureCanManageBuildingAsync(id);

        using (_dataFilter.Disable<ISoftDelete>())
        {
            var building = await _buildingRepository.GetAsync(id);
            var batchId = building.DeletionBatchId;

            if (batchId is null)
            {
                building.IsDeleted = false;
                await _buildingRepository.UpdateAsync(building);
                await CurrentUnitOfWork!.SaveChangesAsync();
                return;
            }

            var candidateFloors = await _floorRepository.GetListAsync(f => f.BuildingId == id);
            var restoredFloors = _spaceHierarchyManager.SelectAndClearForRestore(batchId.Value, candidateFloors);

            var floorIds = restoredFloors.Select(f => f.Id).ToList();
            var candidateSpaces = floorIds.Count == 0
                ? new List<Space>()
                : await _spaceRepository.GetListAsync(s => floorIds.Contains(s.FloorId));
            var restoredSpaces = _spaceHierarchyManager.SelectAndClearForRestore(batchId.Value, candidateSpaces);

            building.ClearDeletionBatch();
            building.IsDeleted = false;
            await _buildingRepository.UpdateAsync(building);

            foreach (var floor in restoredFloors)
            {
                floor.IsDeleted = false;
                await _floorRepository.UpdateAsync(floor);
            }

            foreach (var space in restoredSpaces)
            {
                space.IsDeleted = false;
                await _spaceRepository.UpdateAsync(space);
            }

            await CurrentUnitOfWork!.SaveChangesAsync();
        }
    }

    private async Task<List<string>> FindNarrowingConflictsAsync(Guid buildingId, OperatingDays proposedDays, OperatingWindow proposedHours)
    {
        var floors = (await _floorRepository.GetListAsync(f => f.BuildingId == buildingId, includeDetails: true))
            .Where(f => f.Days is not null || f.Hours is not null)
            .ToList();
        var names = await _nameReader.ShownAsync<Floor, FloorTranslation>(floors);
        var candidates = floors.Select(f => new NarrowingCandidate(names[f.Id], f.Days, f.Hours)).ToList();

        return _constraintResolver.FindNarrowingConflicts(candidates, proposedDays, proposedHours).ToList();
    }

    private static Task EnsureCanManageBuildingAsync(Guid buildingId)
    {
        // Extensibility hook for future per-building-admin scoping. Floor/Space's equivalents
        // and any building-scoped permission plug in here without touching call sites. It
        // deliberately checks nothing today: each method's [Authorize] names what it needs,
        // and re-checking Edit here refused roles that only had Create or Delete.
        _ = buildingId;
        return Task.CompletedTask;
    }

    /// <summary>By language; a language given twice keeps its last address.</summary>
    private static Dictionary<string, string?> ToAddressMap(IEnumerable<BuildingAddressDto> addresses) =>
        addresses
            .GroupBy(a => a.Language)
            .ToDictionary(g => g.Key, g => (string?)g.Last().Address);

    private async Task<BuildingDto> MapToDtoAsync(Building building)
    {
        var dto = ObjectMapper.Map<Building, BuildingDto>(building);
        dto.Name = await _nameReader.ShownAsync(building);
        dto.Names = building.Translations.ToNameDtos();
        dto.Addresses = building.Translations
            .Where(t => t.Address is not null)
            .OrderBy(t => t.Language, StringComparer.Ordinal)
            .Select(t => new BuildingAddressDto { Language = t.Language, Address = t.Address! })
            .ToList();
        dto.Days = ConstraintDtoConversions.ToDayArray(building.Days);
        dto.Hours = ConstraintDtoConversions.ToWindowDto(building.Hours);
        dto.IsDeleted = building.IsDeleted;
        return dto;
    }
}
