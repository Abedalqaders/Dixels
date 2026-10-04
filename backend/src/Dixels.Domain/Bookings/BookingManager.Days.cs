using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;

namespace Dixels.Bookings;

public partial class BookingManager
{
    /// <summary>
    /// A space's days from <paramref name="from"/> to <paramref name="to"/> (building-local
    /// dates): open times, closures and bookings, so the booking form offers only times that
    /// can be booked. The range is cut to today … the last date the building lets you book,
    /// and bookings and closures for all of it are loaded in one query each.
    /// </summary>
    public async Task<SpaceDays> GetSpaceDaysAsync(Guid userId, Guid spaceId, DateOnly from, DateOnly to)
    {
        // GetAsync respects the soft-delete filter: a deleted space is a plain 404.
        var space = await _spaceRepository.GetAsync(spaceId);
        var floor = await _floorRepository.GetAsync(space.FloorId);
        var building = await _buildingRepository.GetAsync(floor.BuildingId);

        await _accessChecker.EnsureCanBookAsync(userId, building.Id);

        var clock = new BuildingClock(building.Timezone);
        var now = new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
        var today = clock.LocalDate(now);
        var lastDate = today.AddDays(building.MaxHorizonDays);

        from = from < today ? today : from;
        to = to > lastDate ? lastDate : to;
        if (to < from)
        {
            return new SpaceDays(clock, Array.Empty<SpaceDay>());
        }

        var rangeStart = clock.StartOfLocalDay(from);
        var rangeEnd = clock.StartOfLocalDay(to.AddDays(1));
        var floorId = floor.Id;
        var buildingId = building.Id;

        // Closures union across levels: the space's own, its floor's, and the building's.
        var overrides = (await _overrideRepository.GetListAsync(o =>
                ((o.Scope == OverrideScope.Space && o.ScopeId == spaceId)
                 || (o.Scope == OverrideScope.Floor && o.ScopeId == floorId)
                 || (o.Scope == OverrideScope.Building && o.ScopeId == buildingId))
                && o.StartsAt < rangeEnd
                && o.EndsAt > rangeStart))
            .Select(OverrideWindow.From)
            .ToList();

        var busy = (await _bookingRepository.GetConfirmedOverlappingAsync(new[] { spaceId }, rangeStart, rangeEnd))
            .Select(b => new BusyRange(new TimeRange(b.StartsAt, b.EndsAt), b.UserId == userId))
            .ToList();

        var rules = _constraintResolver.Resolve(building, floor, space);
        var days = new List<SpaceDay>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var day = new TimeRange(clock.StartOfLocalDay(date), clock.StartOfLocalDay(date.AddDays(1)));
            var (open, closed) = OpenAndClosedOn(rules, clock, date, day, overrides.Where(o => o.Range.Overlaps(day.Start, day.End)));

            days.Add(new SpaceDay(date, day, open, closed, busy.Where(b => b.Range.Overlaps(day.Start, day.End)).ToList()));
        }

        return new SpaceDays(clock, days);
    }
}
