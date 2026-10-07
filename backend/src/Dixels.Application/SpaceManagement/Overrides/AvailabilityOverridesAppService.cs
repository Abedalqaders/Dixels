using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Reservations;
using Dixels.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
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

    public async Task<PagedResultDto<AvailabilityOverrideDto>> GetListAsync(GetAvailabilityOverridesInput input)
    {
        var now = Now();
        var queryable = (await _overrideRepository.GetQueryableAsync())
            .Where(o => o.Scope == input.Scope && o.ScopeId == input.ScopeId);
        if (!input.IncludePast)
        {
            queryable = queryable.Where(o => o.EndsAt > now);
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var page = await AsyncExecuter.ToListAsync(
            (input.IncludePast ? queryable.OrderByDescending(o => o.StartsAt) : queryable.OrderBy(o => o.StartsAt))
                .ThenBy(o => o.Id)
                .PageBy(input));

        return new PagedResultDto<AvailabilityOverrideDto>(
            totalCount, page.Select(o => ObjectMapper.Map<AvailabilityOverride, AvailabilityOverrideDto>(o)).ToList());
    }

    public async Task<ListResultDto<AvailabilityOverrideDto>> GetActiveAsync(OverrideScope scope, Guid scopeId)
    {
        var now = Now();
        var active = await _overrideRepository.GetListAsync(
            o => o.Scope == scope && o.ScopeId == scopeId && o.StartsAt <= now && o.EndsAt > now);

        return new ListResultDto<AvailabilityOverrideDto>(
            active.OrderBy(o => o.StartsAt).Select(o => ObjectMapper.Map<AvailabilityOverride, AvailabilityOverrideDto>(o)).ToList());
    }

    private DateTimeOffset Now() => new(Clock.Now.ToUniversalTime(), TimeSpan.Zero);

    [Authorize(DixelsPermissions.Overrides.Create)]
    public async Task<AvailabilityOverrideDto> CreateAsync(CreateAvailabilityOverrideDto input)
    {
        // Where it is, read once; null when the scope doesn't exist (nothing to announce there).
        var place = await FindPlaceAsync(input);

        // The rooms it closes, worked out before it's saved; none for a special opening, which
        // can't break anything. When the admin chose to cancel what it closes, the bookings it
        // breaks are found now (the closure unsaved) and carried by the event, so no listener
        // checks them all again; kept bookings need no check, so then only the rooms' ids are read.
        List<Guid>? closedRooms = null;
        IReadOnlyList<AffectedReservation>? affected = null;
        if (place is not null && Closes(input))
        {
            if (input.CancelAffectedBookings)
            {
                var change = await ProposedClosureAsync(input, place);
                affected = await _impactPreview.AffectedAsync(change);
                closedRooms = change.Rooms.Select(r => r.Space.Id).ToList();
            }
            else
            {
                closedRooms = await LazyServiceProvider.LazyGetRequiredService<RoomIdReader>().GetListAsync(place.Scope);
            }
        }

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

        if (closedRooms is not null)
        {
            // What the rooms hold is released (or kept, as the admin chose) by its own module.
            await _localEventBus.PublishAsync(new ClosureCreatedEvent(
                availabilityOverride.Id,
                place!.Building.Id,
                closedRooms,
                ClosureReason(input),
                input.CancelAffectedBookings,
                CurrentUser.GetId(),
                affected));
        }

        return ObjectMapper.Map<AvailabilityOverride, AvailabilityOverrideDto>(availabilityOverride);
    }

    [Authorize(DixelsPermissions.Overrides.Create)]
    public async Task<ReservationImpactDto> GetCreateImpactAsync(CreateAvailabilityOverrideDto input, int skip = 0)
    {
        if (!Closes(input))
        {
            return new ReservationImpactDto();
        }

        var place = await FindPlaceAsync(input) ?? throw NotFound(input);
        return await _impactPreview.NoLongerFittingAsync(await ProposedClosureAsync(input, place), skip);
    }

    // "Closed: Replacing the chair" — the admin's own words when there are any, else the category.
    private string ClosureReason(CreateAvailabilityOverrideDto input) =>
        L[
            "Dixels:Bookings:CancelReason:Closure",
            string.IsNullOrWhiteSpace(input.ReasonDetail) ? L["Enum:ReasonCategory." + input.ReasonCategory].Value : input.ReasonDetail.Trim()].Value;

    /// <summary>A closure over a real range: a special opening, or an empty range, closes nothing.</summary>
    private static bool Closes(CreateAvailabilityOverrideDto input) =>
        input.Effect == OverrideEffect.Closed && input.EndsAt > input.StartsAt;

    /// <summary>Where an override is: its building, and the floor or room when it's on one.</summary>
    private sealed record Place(Building Building, Floor? Floor, Space? Space)
    {
        public RoomScope Scope => new(Building.Id, Floor?.Id, Space?.Id);
    }

    /// <summary>The override's scope with what it's in, read once; null when the scope doesn't exist.</summary>
    private async Task<Place?> FindPlaceAsync(CreateAvailabilityOverrideDto input)
    {
        switch (input.Scope)
        {
            case OverrideScope.Space:
            {
                if (await _spaceRepository.FindAsync(input.ScopeId) is not { } space)
                {
                    return null;
                }

                var floor = await _floorRepository.GetAsync(space.FloorId);
                return new Place(await _buildingRepository.GetAsync(floor.BuildingId), floor, space);
            }
            case OverrideScope.Floor:
            {
                if (await _floorRepository.FindAsync(input.ScopeId) is not { } floor)
                {
                    return null;
                }

                return new Place(await _buildingRepository.GetAsync(floor.BuildingId), floor, null);
            }
            default:
                return await _buildingRepository.FindAsync(input.ScopeId) is { } building ? new Place(building, null, null) : null;
        }
    }

    /// <summary>The not-found GetAsync gives for the scope's own type, so a missing scope reads the same as before.</summary>
    private static EntityNotFoundException NotFound(CreateAvailabilityOverrideDto input) => input.Scope switch
    {
        OverrideScope.Space => new EntityNotFoundException(typeof(Space), input.ScopeId),
        OverrideScope.Floor => new EntityNotFoundException(typeof(Floor), input.ScopeId),
        _ => new EntityNotFoundException(typeof(Building), input.ScopeId),
    };

    /// <summary>The rooms a new closure reaches, with their rules as they are and the closure unsaved.</summary>
    private async Task<RoomRulesChange> ProposedClosureAsync(CreateAvailabilityOverrideDto input, Place place)
    {
        List<(Space Space, Floor Floor)> rooms;
        if (place.Space is { } space)
        {
            rooms = new List<(Space, Floor)> { (space, place.Floor!) };
        }
        else if (place.Floor is { } floor)
        {
            rooms = (await _spaceRepository.GetListAsync(s => s.FloorId == floor.Id)).Select(s => (s, floor)).ToList();
        }
        else
        {
            var floors = (await _floorRepository.GetListAsync(f => f.BuildingId == place.Building.Id)).ToDictionary(f => f.Id);
            var floorIds = floors.Keys.ToList();
            rooms = floorIds.Count == 0
                ? new List<(Space, Floor)>()
                : (await _spaceRepository.GetListAsync(s => floorIds.Contains(s.FloorId))).Select(s => (s, floors[s.FloorId])).ToList();
        }

        var building = place.Building;
        var closure = new AvailabilityOverride(
            Guid.Empty, input.Scope, input.ScopeId, input.StartsAt, input.EndsAt, OverrideEffect.Closed, input.ReasonCategory, input.ReasonDetail);

        return new RoomRulesChange(building, rooms, (s, f) => _constraintResolver.Resolve(building, f, s), closure);
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
