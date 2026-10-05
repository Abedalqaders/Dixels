using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
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

[Authorize(DixelsPermissions.Spaces.Default)]
public class SpacesAppService : DixelsAppService, ISpacesAppService
{
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;
    private readonly ConstraintResolver _constraintResolver;
    private readonly IDataFilter _dataFilter;
    private readonly BookingImpactService _bookingImpact;
    private readonly ILocalEventBus _localEventBus;
    private readonly LocalizedNameValidator _nameValidator;
    private readonly LocalizedNameReader _nameReader;

    public SpacesAppService(
        IRepository<Space, Guid> spaceRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        IRepository<SpaceType, Guid> spaceTypeRepository,
        ConstraintResolver constraintResolver,
        IDataFilter dataFilter,
        BookingImpactService bookingImpact,
        ILocalEventBus localEventBus,
        LocalizedNameValidator nameValidator,
        LocalizedNameReader nameReader)
    {
        _spaceRepository = spaceRepository;
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _spaceTypeRepository = spaceTypeRepository;
        _constraintResolver = constraintResolver;
        _dataFilter = dataFilter;
        _bookingImpact = bookingImpact;
        _localEventBus = localEventBus;
        _nameValidator = nameValidator;
        _nameReader = nameReader;
    }

    public async Task<SpaceDto> GetAsync(Guid id)
    {
        var space = await _spaceRepository.GetAsync(id);
        return await MapToDtoAsync(space);
    }

    public async Task<PagedResultDto<SpaceDto>> GetListAsync(GetSpacesInput input)
    {
        // Space has no EF navigation to Floor/Building (separate aggregate roots, FK-only),
        // so FloorName/BuildingName are resolved with an explicit double join — one SQL
        // query, not one lookup per row. Joined unconditionally for the same reason as
        // FloorsAppService's own GetListAsync: one code path for both the standalone Spaces
        // page and the drill-down Spaces-of-a-floor page.
        //
        // Names: search matches a name in any language, ignoring case; rows are sorted by the
        // names the reader sees (theirs, else the default language's), worked out in the query.
        var (shown, fallback) = await _nameReader.GetLanguagesAsync();
        var term = input.Filter.IsNullOrWhiteSpace() ? null : NameTranslation.Normalize(input.Filter!);

        async Task<PagedResultDto<SpaceDto>> QueryAsync()
        {
            var spacesQueryable = await _spaceRepository.WithDetailsAsync();
            var floorsQueryable = await _floorRepository.GetQueryableAsync();
            var buildingsQueryable = await _buildingRepository.GetQueryableAsync();

            var joined =
                from s in spacesQueryable
                join f in floorsQueryable on s.FloorId equals f.Id
                join b in buildingsQueryable on f.BuildingId equals b.Id
                where term == null
                    || s.Translations.Any(t => t.NormalizedName.Contains(term))
                    || f.Translations.Any(t => t.NormalizedName.Contains(term))
                    || b.Translations.Any(t => t.NormalizedName.Contains(term))
                select new
                {
                    Space = s,
                    SpaceName = s.Translations.Where(t => t.Language == shown).Select(t => t.Name).FirstOrDefault()
                        ?? s.Translations.Where(t => t.Language == fallback).Select(t => t.Name).FirstOrDefault(),
                    FloorName = f.Translations.Where(t => t.Language == shown).Select(t => t.Name).FirstOrDefault()
                        ?? f.Translations.Where(t => t.Language == fallback).Select(t => t.Name).FirstOrDefault(),
                    BuildingName = b.Translations.Where(t => t.Language == shown).Select(t => t.Name).FirstOrDefault()
                        ?? b.Translations.Where(t => t.Language == fallback).Select(t => t.Name).FirstOrDefault(),
                    BuildingId = b.Id,
                };

            if (input.FloorId.HasValue)
            {
                joined = joined.Where(x => x.Space.FloorId == input.FloorId.Value);
            }

            if (input.BuildingId.HasValue)
            {
                joined = joined.Where(x => x.BuildingId == input.BuildingId.Value);
            }

            if (input.SpaceTypeId.HasValue)
            {
                joined = joined.Where(x => x.Space.SpaceTypeId == input.SpaceTypeId.Value);
            }

            var totalCount = await AsyncExecuter.CountAsync(joined);
            var page = await AsyncExecuter.ToListAsync(
                joined.OrderBy(x => x.BuildingName).ThenBy(x => x.FloorName).ThenBy(x => x.SpaceName)
                    .Skip(input.SkipCount).Take(input.MaxResultCount));

            var items = new List<SpaceDto>();
            foreach (var x in page)
            {
                var dto = await MapToDtoAsync(x.Space);
                dto.FloorName = x.FloorName;
                dto.BuildingName = x.BuildingName;
                items.Add(dto);
            }

            return new PagedResultDto<SpaceDto>(totalCount, items);
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

    [Authorize(DixelsPermissions.Spaces.Create)]
    public async Task<SpaceDto> CreateAsync(CreateSpaceDto input)
    {
        await EnsureCanManageBuildingAsync(input.FloorId);

        // GetAsync answers an unknown or soft-deleted id with a 404 — otherwise the missing
        // parent surfaces as a foreign-key failure from the database, i.e. a 500.
        await _floorRepository.GetAsync(input.FloorId);
        await _spaceTypeRepository.GetAsync(input.SpaceTypeId);

        var names = await _nameValidator.NormalizeAsync(input.Names.ToNames());
        var space = new Space(GuidGenerator.Create(), input.FloorId, names[0].Language, names[0].Name, input.SpaceTypeId, input.Capacity);
        space.SetNames(names);
        await _spaceRepository.InsertAsync(space);

        return await MapToDtoAsync(space);
    }

    [Authorize(DixelsPermissions.Spaces.Edit)]
    public async Task<SpaceDto> UpdateAsync(Guid id, UpdateSpaceDto input)
    {
        var space = await _spaceRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(space.FloorId);

        var broken = input.CancelAffectedBookings
            ? await FindBookingsOverCapacityAsync(space, input.Capacity)
            : Array.Empty<BookingImpact>();

        space.SetNames(await _nameValidator.NormalizeAsync(input.Names.ToNames()));
        if (space.SpaceTypeId != input.SpaceTypeId)
        {
            await _spaceTypeRepository.GetAsync(input.SpaceTypeId); // 404 for unknown or deleted
        }
        space.SetSpaceType(input.SpaceTypeId);
        space.SetCapacity(input.Capacity);

        await _spaceRepository.UpdateAsync(space, autoSave: true);
        await _bookingImpact.CancelForRuleChangeAsync(broken, CurrentUser.GetId());

        return await MapToDtoAsync(space);
    }

    [Authorize(DixelsPermissions.Spaces.Edit)]
    public async Task<BookingImpactDto> GetUpdateImpactAsync(Guid id, UpdateSpaceDto input)
    {
        var space = await _spaceRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(space.FloorId);
        var floor = await _floorRepository.GetAsync(space.FloorId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);
        return await _bookingImpact.DescribeAsync(building, await FindBookingsOverCapacityAsync(space, input.Capacity));
    }

    // Only the capacity can break a booking among the details; checked on an untracked copy
    // carrying the room's own rules, so nothing here saves.
    private async Task<IReadOnlyList<BookingImpact>> FindBookingsOverCapacityAsync(Space space, int capacity)
    {
        if (capacity >= space.Capacity)
        {
            return Array.Empty<BookingImpact>();
        }

        var floor = await _floorRepository.GetAsync(space.FloorId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);
        var parent = _constraintResolver.Resolve(building, floor);

        var proposed = UntrackedCopy(space);
        proposed.SetOwnOperatingDays(space.Days, parent.Days.Value);
        proposed.SetOwnOperatingHours(space.Hours, parent.Hours.Value);
        proposed.SetOwnMaxDuration(space.MaxDurationMinutes);
        proposed.SetMinAttendees(space.MinAttendees);
        proposed.SetCapacity(capacity);

        return await _bookingImpact.Checker.FindNoLongerFittingAsync(
            building, new[] { (space, floor) }, (_, _) => _constraintResolver.Resolve(building, floor, proposed));
    }

    [Authorize(DixelsPermissions.Spaces.Edit)]
    public async Task<ConstraintsSaveResultDto> UpdateConstraintsAsync(Guid id, UpdateSpaceConstraintsDto input)
    {
        var space = await _spaceRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(space.FloorId);

        var floor = await _floorRepository.GetAsync(space.FloorId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);

        // Narrowed against the *resolved* Floor value (Floor's own override if set, else the
        // Building's) — reusing the same resolver as the read path, never the raw Floor row.
        var resolvedParent = _constraintResolver.Resolve(building, floor);

        space.ConcurrencyStamp = input.ConcurrencyStamp;

        var proposedDays = ConstraintDtoConversions.ToOperatingDaysOrNull(input.Days);
        var proposedHours = ConstraintDtoConversions.ToOperatingWindowOrNull(input.Hours);

        var broken = input.CancelAffectedBookings
            ? await FindBrokenBookingsAsync(building, floor, space, input)
            : Array.Empty<BookingImpact>();

        space.SetOwnOperatingDays(proposedDays, resolvedParent.Days.Value);
        space.SetOwnOperatingHours(proposedHours, resolvedParent.Hours.Value);
        space.SetOwnMaxDuration(input.MaxDurationMinutes);
        space.SetMinAttendees(input.MinAttendees);

        await _spaceRepository.UpdateAsync(space);
        await CurrentUnitOfWork!.SaveChangesAsync();
        await _bookingImpact.CancelForRuleChangeAsync(broken, CurrentUser.GetId());

        return new ConstraintsSaveResultDto
        {
            ConcurrencyStamp = space.ConcurrencyStamp,
            // A Space is a leaf — nothing sits below it to ever produce a narrowing warning.
            Warnings = new List<string>(),
            CancelledBookings = broken.Count,
        };
    }

    [Authorize(DixelsPermissions.Spaces.Edit)]
    public async Task<BookingImpactDto> GetConstraintsImpactAsync(Guid id, UpdateSpaceConstraintsDto input)
    {
        var space = await _spaceRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(space.FloorId);
        var floor = await _floorRepository.GetAsync(space.FloorId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);
        return await _bookingImpact.DescribeAsync(building, await FindBrokenBookingsAsync(building, floor, space, input));
    }

    [Authorize(DixelsPermissions.Spaces.Delete)]
    public async Task<BookingImpactDto> GetDeleteImpactAsync(Guid id)
    {
        var space = await _spaceRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(space.FloorId);
        var floor = await _floorRepository.GetAsync(space.FloorId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);
        return await _bookingImpact.DescribeAsync(
            building,
            await _bookingImpact.UpcomingAsync(new[] { (space, floor) }),
            _bookingImpact.Text("Dixels:Bookings:CancelReason:SpaceRemoved"));
    }

    // The proposed space is a fresh, untracked copy — checking it can never save anything.
    private async Task<IReadOnlyList<BookingImpact>> FindBrokenBookingsAsync(Building building, Floor floor, Space space, UpdateSpaceConstraintsDto input)
    {
        var resolvedParent = _constraintResolver.Resolve(building, floor);
        var proposed = UntrackedCopy(space);
        proposed.SetOwnOperatingDays(ConstraintDtoConversions.ToOperatingDaysOrNull(input.Days), resolvedParent.Days.Value);
        proposed.SetOwnOperatingHours(ConstraintDtoConversions.ToOperatingWindowOrNull(input.Hours), resolvedParent.Hours.Value);
        proposed.SetOwnMaxDuration(input.MaxDurationMinutes);
        proposed.SetMinAttendees(input.MinAttendees);

        return await _bookingImpact.Checker.FindNoLongerFittingAsync(
            building, new[] { (space, floor) }, (_, _) => _constraintResolver.Resolve(building, floor, proposed));
    }

    public async Task<ResolvedConstraintsDto> GetResolvedConstraintsAsync(Guid id)
    {
        var space = await _spaceRepository.GetAsync(id);
        var floor = await _floorRepository.GetAsync(space.FloorId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);

        var resolved = _constraintResolver.Resolve(building, floor, space);

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
            FloorId = floor.Id,
            FloorName = await _nameReader.ShownAsync(floor),
        };
    }

    [Authorize(DixelsPermissions.Spaces.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var space = await _spaceRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(space.FloorId);

        // No DeletionBatchId stamping here, unlike Building/Floor: batch scoping exists only
        // to disambiguate cascaded siblings on restore, and a Space is a leaf with nothing
        // below it to disambiguate against.
        await _spaceRepository.DeleteAsync(space);
        await CurrentUnitOfWork!.SaveChangesAsync();

        // What it held (bookings) is released by its own module.
        await _localEventBus.PublishAsync(new SpaceDeletedEvent(id, CurrentUser.GetId()));
    }

    [Authorize(DixelsPermissions.Spaces.Edit)]
    public async Task RestoreAsync(Guid id)
    {
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var space = await _spaceRepository.GetAsync(id);
            await EnsureCanManageBuildingAsync(space.FloorId);

            // A space only makes sense under a live floor (a floor under a deleted building is
            // itself deleted by the cascade, so this one check covers both levels).
            var floor = await _floorRepository.GetAsync(space.FloorId);
            if (floor.IsDeleted)
            {
                throw new BusinessException(DixelsDomainErrorCodes.ParentIsDeleted).WithData("parent", "floor");
            }

            space.IsDeleted = false;
            await _spaceRepository.UpdateAsync(space);
            await CurrentUnitOfWork!.SaveChangesAsync();
        }
    }

    private static Task EnsureCanManageBuildingAsync(Guid floorId)
    {
        // Same extensibility hook as Buildings/FloorsAppService's: the one place a future
        // per-building-admin check plugs in. It must not re-check a flat permission — each
        // method's own [Authorize] already names what it needs, and requiring Edit here
        // silently refused roles that only had Create or Delete.
        _ = floorId;
        return Task.CompletedTask;
    }

    // A fresh, untracked copy to check a proposed change against — checking it can never save
    // anything. Its name plays no part in the check, so any one of them will do.
    private static Space UntrackedCopy(Space space)
    {
        var name = space.Translations.First();
        return new Space(space.Id, space.FloorId, name.Language, name.Name, space.SpaceTypeId, space.Capacity);
    }

    private async Task<SpaceDto> MapToDtoAsync(Space space)
    {
        var dto = ObjectMapper.Map<Space, SpaceDto>(space);
        dto.Name = await _nameReader.ShownAsync(space);
        dto.Names = space.Translations.ToNameDtos();
        dto.Days = ConstraintDtoConversions.ToDayArrayOrNull(space.Days);
        dto.Hours = ConstraintDtoConversions.ToWindowDtoOrNull(space.Hours);
        dto.HasOverrides = space.Days is not null || space.Hours is not null
            || space.MaxDurationMinutes is not null || space.MinAttendees is not null;
        dto.IsDeleted = space.IsDeleted;
        return dto;
    }
}
