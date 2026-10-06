using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Reservations;
using Dixels.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
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
    private readonly ReservationImpactPreview _impactPreview;
    private readonly ILocalEventBus _localEventBus;

    public AvailabilityOverridesAppService(
        IRepository<AvailabilityOverride, Guid> overrideRepository,
        IRepository<Building, Guid> buildingRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Space, Guid> spaceRepository,
        ConstraintResolver constraintResolver,
        ReservationImpactPreview impactPreview,
        ILocalEventBus localEventBus)
    {
        _overrideRepository = overrideRepository;
        _buildingRepository = buildingRepository;
        _floorRepository = floorRepository;
        _spaceRepository = spaceRepository;
        _constraintResolver = constraintResolver;
        _impactPreview = impactPreview;
        _localEventBus = localEventBus;
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
        // The rooms it closes, worked out before it's saved; null for a special opening, which
        // can't break anything, or a scope that doesn't exist (nothing to announce there).
        var closing = await ScopeExistsAsync(input) ? await ProposedClosureAsync(input) : null;

        var availabilityOverride = new AvailabilityOverride(
            GuidGenerator.Create(),
            input.Scope,
            input.ScopeId,
            input.StartsAt,
            input.EndsAt,
            input.Effect,
            input.ReasonCategory,
            input.ReasonDetail);

        // Saved now, so whoever listens reads it along with the room's other closures.
        await _overrideRepository.InsertAsync(availabilityOverride, autoSave: true);

        if (closing is not null)
        {
            // What the rooms hold is released (or kept, as the admin chose) by its own module.
            await _localEventBus.PublishAsync(new ClosureCreatedEvent(
                availabilityOverride.Id,
                closing.Building.Id,
                closing.Rooms.Select(r => r.Space.Id).ToList(),
                ClosureReason(input),
                input.CancelAffectedBookings,
                CurrentUser.GetId()));
        }

        return ObjectMapper.Map<AvailabilityOverride, AvailabilityOverrideDto>(availabilityOverride);
    }

    [Authorize(DixelsPermissions.Overrides.Create)]
    public async Task<ReservationImpactDto> GetCreateImpactAsync(CreateAvailabilityOverrideDto input)
    {
        var change = await ProposedClosureAsync(input);
        return change is null ? new ReservationImpactDto() : await _impactPreview.NoLongerFittingAsync(change);
    }

    // "Closed: Replacing the chair" — the admin's own words when there are any, else the category.
    private string ClosureReason(CreateAvailabilityOverrideDto input) =>
        L[
            "Dixels:Bookings:CancelReason:Closure",
            string.IsNullOrWhiteSpace(input.ReasonDetail) ? L["Enum:ReasonCategory." + input.ReasonCategory].Value : input.ReasonDetail.Trim()].Value;

    /// <summary>
    /// The rooms a new closure reaches, with their rules as they are and the closure unsaved;
    /// null when it closes nothing (a special opening, or an empty range).
    /// </summary>
    private Task<bool> ScopeExistsAsync(CreateAvailabilityOverrideDto input) => input.Scope switch
    {
        OverrideScope.Space => _spaceRepository.AnyAsync(s => s.Id == input.ScopeId),
        OverrideScope.Floor => _floorRepository.AnyAsync(f => f.Id == input.ScopeId),
        _ => _buildingRepository.AnyAsync(b => b.Id == input.ScopeId),
    };

    private async Task<RoomRulesChange?> ProposedClosureAsync(CreateAvailabilityOverrideDto input)
    {
        if (input.Effect != OverrideEffect.Closed || input.EndsAt <= input.StartsAt)
        {
            return null;
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

        var closure = new AvailabilityOverride(
            Guid.Empty, input.Scope, input.ScopeId, input.StartsAt, input.EndsAt, OverrideEffect.Closed, input.ReasonCategory, input.ReasonDetail);

        return new RoomRulesChange(building, rooms, (space, floor) => _constraintResolver.Resolve(building, floor, space), closure);
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
