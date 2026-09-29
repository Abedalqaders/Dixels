using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Permissions;
using Dixels.SpaceManagement.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
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
    private readonly IDataFilter _dataFilter;
    private readonly BookingImpactService _bookingImpact;

    public FloorsAppService(
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        IRepository<Space, Guid> spaceRepository,
        ConstraintResolver constraintResolver,
        SpaceHierarchyManager spaceHierarchyManager,
        IDataFilter dataFilter,
        BookingImpactService bookingImpact)
    {
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _spaceRepository = spaceRepository;
        _constraintResolver = constraintResolver;
        _spaceHierarchyManager = spaceHierarchyManager;
        _dataFilter = dataFilter;
        _bookingImpact = bookingImpact;
    }

    public async Task<FloorDto> GetAsync(Guid id)
    {
        await CheckAnyPermissionAsync(DixelsPermissions.Readers.Floors);
        var floor = await _floorRepository.GetAsync(id);
        return MapToDto(floor);
    }

    public async Task<PagedResultDto<FloorDto>> GetListAsync(GetFloorsInput input)
    {
        await CheckAnyPermissionAsync(DixelsPermissions.Readers.Floors);

        // Floor has no EF navigation to Building (separate aggregate roots, FK-only), so
        // BuildingName is resolved with an explicit join — one SQL query, not one lookup per
        // row. Joining unconditionally (even when BuildingId scopes to one building) keeps a
        // single code path instead of branching on whether the caller is the standalone
        // Floors page or the drill-down Floors-of-a-building page.
        async Task<PagedResultDto<FloorDto>> QueryAsync()
        {
            var floorsQueryable = await _floorRepository.GetQueryableAsync();
            var buildingsQueryable = await _buildingRepository.GetQueryableAsync();

            var joined =
                from f in floorsQueryable
                join b in buildingsQueryable on f.BuildingId equals b.Id
                select new { Floor = f, BuildingName = b.Name };

            if (input.BuildingId.HasValue)
            {
                joined = joined.Where(x => x.Floor.BuildingId == input.BuildingId.Value);
            }

            if (!input.Filter.IsNullOrWhiteSpace())
            {
                joined = input.FloorNameOnly
                    ? joined.Where(x => x.Floor.Name.Contains(input.Filter!))
                    : joined.Where(x => x.Floor.Name.Contains(input.Filter!) || x.BuildingName.Contains(input.Filter!));
            }

            var totalCount = await AsyncExecuter.CountAsync(joined);
            var page = await AsyncExecuter.ToListAsync(
                joined.OrderBy(x => x.BuildingName).ThenBy(x => x.Floor.Name).Skip(input.SkipCount).Take(input.MaxResultCount));

            var items = page.Select(x =>
            {
                var dto = MapToDto(x.Floor);
                dto.BuildingName = x.BuildingName;
                return dto;
            }).ToList();

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

        var floor = new Floor(GuidGenerator.Create(), input.BuildingId, input.Name, input.FloorNumber);
        await _floorRepository.InsertAsync(floor);

        return MapToDto(floor);
    }

    [Authorize(DixelsPermissions.Floors.Edit)]
    public async Task<FloorDto> UpdateAsync(Guid id, UpdateFloorDto input)
    {
        var floor = await _floorRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(floor.BuildingId);

        floor.SetName(input.Name);
        floor.SetFloorNumber(input.FloorNumber);

        await _floorRepository.UpdateAsync(floor);

        return MapToDto(floor);
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

        // Computed before saving — the tightening itself is never blocked, this is purely
        // informational for the admin to go fix the named Spaces afterward.
        var warnings = await FindNarrowingConflictsAsync(id, proposedDays, proposedHours);

        var broken = input.CancelAffectedBookings
            ? await FindBrokenBookingsAsync(building, floor, input)
            : Array.Empty<BookingImpact>();

        // Validated against the Building's raw values directly — Building has no parent of
        // its own, so its own fields already are the "resolved" value.
        floor.SetOwnOperatingDays(proposedDays, building.Days);
        floor.SetOwnOperatingHours(proposedHours, building.Hours);
        floor.SetOwnMaxDuration(input.MaxDurationMinutes);

        await _floorRepository.UpdateAsync(floor);
        await CurrentUnitOfWork!.SaveChangesAsync();
        await _bookingImpact.CancelForRuleChangeAsync(broken, CurrentUser.GetId());

        return new ConstraintsSaveResultDto
        {
            ConcurrencyStamp = floor.ConcurrencyStamp,
            Warnings = warnings,
            CancelledBookings = broken.Count,
        };
    }

    [Authorize(DixelsPermissions.Floors.Edit)]
    public async Task<BookingImpactDto> GetConstraintsImpactAsync(Guid id, UpdateFloorConstraintsDto input)
    {
        var floor = await _floorRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(floor.BuildingId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);
        return await _bookingImpact.DescribeAsync(building, await FindBrokenBookingsAsync(building, floor, input));
    }

    [Authorize(DixelsPermissions.Floors.Delete)]
    public async Task<BookingImpactDto> GetDeleteImpactAsync(Guid id)
    {
        var floor = await _floorRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(floor.BuildingId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);
        return await _bookingImpact.DescribeAsync(
            building,
            await _bookingImpact.UpcomingAsync(await RoomsAsync(floor)),
            _bookingImpact.Text("Dixels:Bookings:CancelReason:FloorRemoved"));
    }

    // The proposed floor is a fresh, untracked copy — checking it can never save anything.
    private async Task<IReadOnlyList<BookingImpact>> FindBrokenBookingsAsync(Building building, Floor floor, UpdateFloorConstraintsDto input)
    {
        var proposed = new Floor(floor.Id, floor.BuildingId, floor.Name, floor.FloorNumber);
        proposed.SetOwnOperatingDays(ConstraintDtoConversions.ToOperatingDaysOrNull(input.Days), building.Days);
        proposed.SetOwnOperatingHours(ConstraintDtoConversions.ToOperatingWindowOrNull(input.Hours), building.Hours);
        proposed.SetOwnMaxDuration(input.MaxDurationMinutes);

        return await _bookingImpact.Checker.FindNoLongerFittingAsync(
            building, await RoomsAsync(floor), (space, _) => _constraintResolver.Resolve(building, proposed, space));
    }

    private async Task<List<(Space Space, Floor Floor)>> RoomsAsync(Floor floor) =>
        (await _spaceRepository.GetListAsync(s => s.FloorId == floor.Id)).Select(s => (s, floor)).ToList();

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
            BuildingName = building.Name,
        };
    }

    [Authorize(DixelsPermissions.Floors.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var floor = await _floorRepository.GetAsync(id);
        await EnsureCanManageBuildingAsync(floor.BuildingId);

        var spaces = await _spaceRepository.GetListAsync(s => s.FloorId == id);
        var upcoming = await _bookingImpact.UpcomingAsync(spaces.Select(s => (s, floor)).ToList());

        var batchId = GuidGenerator.Create();
        _spaceHierarchyManager.MarkForSoftDelete(batchId, floor, spaces);

        // Flushed separately from the soft-deletes below — see BuildingsAppService.DeleteAsync
        // for why: converting Remove -> Modified for the soft-delete appears to reset which
        // properties EF considers changed, silently dropping a same-transaction DeletionBatchId
        // mutation otherwise.
        await _floorRepository.UpdateAsync(floor);
        foreach (var space in spaces)
        {
            await _spaceRepository.UpdateAsync(space);
        }

        await CurrentUnitOfWork!.SaveChangesAsync();

        foreach (var space in spaces)
        {
            await _spaceRepository.DeleteAsync(space);
        }

        await _floorRepository.DeleteAsync(floor);
        await CurrentUnitOfWork!.SaveChangesAsync();

        await _bookingImpact.CancelAllAsync(upcoming, CurrentUser.GetId(), _bookingImpact.Text("Dixels:Bookings:CancelReason:FloorRemoved"));
    }

    [Authorize(DixelsPermissions.Floors.Edit)]
    public async Task RestoreAsync(Guid id)
    {
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var floor = await _floorRepository.GetAsync(id);
            await EnsureCanManageBuildingAsync(floor.BuildingId);

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

    private async Task<List<string>> FindNarrowingConflictsAsync(Guid floorId, OperatingDays? proposedDays, OperatingWindow? proposedHours)
    {
        var spaces = await _spaceRepository.GetListAsync(s => s.FloorId == floorId);
        var candidates = spaces
            .Where(s => s.Days is not null || s.Hours is not null)
            .Select(s => new NarrowingCandidate(s.Name, s.Days, s.Hours))
            .ToList();

        return _constraintResolver.FindNarrowingConflicts(candidates, proposedDays, proposedHours).ToList();
    }

    private async Task EnsureCanManageBuildingAsync(Guid buildingId)
    {
        // Same extensibility hook as BuildingsAppService's — today just re-checks the flat
        // permission, but is where a future per-building-admin scoping check plugs in,
        // using buildingId rather than the floor's own id.
        _ = buildingId;
        await AuthorizationService.CheckAsync(DixelsPermissions.Floors.Edit);
    }

    private FloorDto MapToDto(Floor floor)
    {
        var dto = ObjectMapper.Map<Floor, FloorDto>(floor);
        dto.Days = ConstraintDtoConversions.ToDayArrayOrNull(floor.Days);
        dto.Hours = ConstraintDtoConversions.ToWindowDtoOrNull(floor.Hours);
        dto.HasOverrides = floor.Days is not null || floor.Hours is not null || floor.MaxDurationMinutes is not null;
        dto.IsDeleted = floor.IsDeleted;
        return dto;
    }
}
