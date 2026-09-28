using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace Dixels.SpaceManagement;

[Authorize(DixelsPermissions.Overrides.Default)]
public class AvailabilityOverridesAppService : DixelsAppService, IAvailabilityOverridesAppService
{
    private readonly IRepository<AvailabilityOverride, Guid> _overrideRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly ConstraintResolver _constraintResolver;
    private readonly BookingImpactService _bookingImpact;

    public AvailabilityOverridesAppService(
        IRepository<AvailabilityOverride, Guid> overrideRepository,
        IRepository<Building, Guid> buildingRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Space, Guid> spaceRepository,
        ConstraintResolver constraintResolver,
        BookingImpactService bookingImpact)
    {
        _overrideRepository = overrideRepository;
        _buildingRepository = buildingRepository;
        _floorRepository = floorRepository;
        _spaceRepository = spaceRepository;
        _constraintResolver = constraintResolver;
        _bookingImpact = bookingImpact;
    }

    public async Task<ListResultDto<AvailabilityOverrideDto>> GetListAsync(OverrideScope scope, Guid scopeId)
    {
        var overrides = await _overrideRepository.GetListAsync(o => o.Scope == scope && o.ScopeId == scopeId);

        return new ListResultDto<AvailabilityOverrideDto>(
            overrides.OrderBy(o => o.StartsAt).Select(o => ObjectMapper.Map<AvailabilityOverride, AvailabilityOverrideDto>(o)).ToList());
    }

    [Authorize(DixelsPermissions.Overrides.Create)]
    public async Task<AvailabilityOverrideDto> CreateAsync(CreateAvailabilityOverrideDto input)
    {
        var (building, broken) = input.CancelAffectedBookings
            ? await FindBrokenBookingsAsync(input)
            : (null, Array.Empty<BookingImpact>());

        var availabilityOverride = new AvailabilityOverride(
            GuidGenerator.Create(),
            input.Scope,
            input.ScopeId,
            input.StartsAt,
            input.EndsAt,
            input.Effect,
            input.ReasonCategory,
            input.ReasonDetail);

        await _overrideRepository.InsertAsync(availabilityOverride);

        if (broken.Count > 0)
        {
            await _bookingImpact.CancelAllAsync(broken, CurrentUser.GetId(), ClosureReason(input));
        }

        return ObjectMapper.Map<AvailabilityOverride, AvailabilityOverrideDto>(availabilityOverride);
    }

    [Authorize(DixelsPermissions.Overrides.Create)]
    public async Task<BookingImpactDto> GetCreateImpactAsync(CreateAvailabilityOverrideDto input)
    {
        var (building, broken) = await FindBrokenBookingsAsync(input);
        return building is null ? new BookingImpactDto() : await _bookingImpact.DescribeAsync(building, broken);
    }

    // "Closed: Replacing the chair" — the admin's own words when there are any, else the category.
    private string ClosureReason(CreateAvailabilityOverrideDto input) =>
        _bookingImpact.Text(
            "Dixels:Bookings:CancelReason:Closure",
            string.IsNullOrWhiteSpace(input.ReasonDetail) ? _bookingImpact.Text("Enum:ReasonCategory." + input.ReasonCategory) : input.ReasonDetail.Trim());

    /// <summary>
    /// The upcoming bookings in the closure's scope (a room, a floor's rooms, or the whole
    /// building's) that the closure would fall on. A special opening can't break anything.
    /// </summary>
    private async Task<(Building? Building, IReadOnlyList<BookingImpact> Broken)> FindBrokenBookingsAsync(CreateAvailabilityOverrideDto input)
    {
        if (input.Effect != OverrideEffect.Closed || input.EndsAt <= input.StartsAt)
        {
            return (null, Array.Empty<BookingImpact>());
        }

        Building building;
        List<(Space Space, Floor Floor)> rooms;
        switch (input.Scope)
        {
            case OverrideScope.Space:
            {
                var space = await _spaceRepository.GetAsync(input.ScopeId);
                var floor = await _floorRepository.GetAsync(space.FloorId);
                building = await _buildingRepository.GetAsync(floor.BuildingId);
                rooms = new List<(Space, Floor)> { (space, floor) };
                break;
            }
            case OverrideScope.Floor:
            {
                var floor = await _floorRepository.GetAsync(input.ScopeId);
                building = await _buildingRepository.GetAsync(floor.BuildingId);
                rooms = (await _spaceRepository.GetListAsync(s => s.FloorId == floor.Id)).Select(s => (s, floor)).ToList();
                break;
            }
            default:
            {
                building = await _buildingRepository.GetAsync(input.ScopeId);
                var floors = (await _floorRepository.GetListAsync(f => f.BuildingId == building.Id)).ToDictionary(f => f.Id);
                var floorIds = floors.Keys.ToList();
                rooms = floorIds.Count == 0
                    ? new List<(Space, Floor)>()
                    : (await _spaceRepository.GetListAsync(s => floorIds.Contains(s.FloorId))).Select(s => (s, floors[s.FloorId])).ToList();
                break;
            }
        }

        var closure = new OverrideWindow(
            new TimeRange(input.StartsAt, input.EndsAt), OverrideEffect.Closed, input.Scope, input.ReasonCategory, input.ReasonDetail);

        var broken = await _bookingImpact.Checker.FindNoLongerFittingAsync(
            building, rooms, (space, floor) => _constraintResolver.Resolve(building, floor, space), closure);
        return (building, broken);
    }

    [Authorize(DixelsPermissions.Overrides.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        // A real hard delete — AvailabilityOverride is AuditedAggregateRoot, not
        // FullAuditedAggregateRoot, so there's no soft-delete interceptor to convert this;
        // matches the entity's own "delete+recreate, not in-place edit" design.
        await _overrideRepository.DeleteAsync(id);
    }
}
