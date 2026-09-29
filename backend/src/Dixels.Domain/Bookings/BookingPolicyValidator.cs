using System;
using System.Collections.Generic;
using System.Linq;
using Dixels.SpaceManagement;
using Volo.Abp.Domain.Services;

namespace Dixels.Bookings;

/// <summary>
/// Checks one booking request against its space's resolved constraints. A pure function:
/// every input, including "now" and whether the slot is already taken, is passed in, so
/// it touches no database and no clock and every rule is unit-testable on its own.
///
/// Returns every violation rather than stopping at the first, biggest blocker first — the
/// UI shows the first one as "why not": someone asking for a room that's closed that day
/// should hear "closed", not "too long" and then be rejected again once they shorten it.
/// Roughly: the date can't be booked at all, then the room isn't open, then it doesn't fit
/// the group, then it's taken, and last what a small tweak fixes (length, slot grid).
/// Precedence (which level's value wins) is already settled by <see cref="ConstraintResolver"/>;
/// this order only decides which rejection is reported first.
/// </summary>
public class BookingPolicyValidator : IDomainService
{
    public IReadOnlyList<BookingViolation> Validate(
        ResolvedConstraints rules,
        BuildingClock clock,
        BookingRequest request,
        IReadOnlyList<OverrideWindow> overrides,
        bool overlapsExistingBooking,
        DateTimeOffset now,
        int slotMinutes)
    {
        var violations = new List<BookingViolation>();

        // The date: in the past, too soon, or beyond how far ahead you may book.
        CheckLeadTime(rules, request, now, violations);
        CheckHorizon(rules, clock, request, now, violations);

        // The room isn't open then: closed by an admin, a closed day, outside the hours.
        CheckClosures(clock, request, overrides, violations);
        CheckDaysAndHours(rules, clock, request, overrides, violations);

        // The room doesn't suit the group.
        CheckCapacity(rules, request, violations);
        CheckMinAttendees(rules, request, violations);

        // Someone has it. (The database's exclusion constraint is the real enforcement — a
        // double-booking can be a race — this is the friendly early answer.)
        if (overlapsExistingBooking)
        {
            violations.Add(new BookingViolation(DixelsDomainErrorCodes.BookingOverlap, null, BookingFormat.Data()));
        }

        // Fixed by a small tweak: shorten it, or snap to the slot grid.
        CheckMaxDuration(rules, request, violations);
        CheckAlignment(clock, request, slotMinutes, violations);

        return violations;
    }

    // Alignment is checked on the local wall clock, not UTC — in a zone offset by a
    // non-whole hour (e.g. UTC+05:45) 09:00 local isn't on a 15-minute UTC boundary.
    private static void CheckAlignment(BuildingClock clock, BookingRequest request, int slotMinutes, List<BookingViolation> violations)
    {
        if (!IsAligned(clock.ToLocal(request.StartUtc), slotMinutes) || !IsAligned(clock.ToLocal(request.EndUtc), slotMinutes))
        {
            violations.Add(new BookingViolation(
                DixelsDomainErrorCodes.BookingNotAligned,
                null,
                BookingFormat.Data(("slotMinutes", slotMinutes))));
        }
    }

    private static bool IsAligned(DateTime local, int slotMinutes)
    {
        return local.Second == 0 && local.Millisecond == 0 && (local.Hour * 60 + local.Minute) % slotMinutes == 0;
    }

    // Closures union: any level can block, so every overlapping Closed override is a
    // violation of its own. An Open override never cancels a closure (closed wins).
    private static void CheckClosures(BuildingClock clock, BookingRequest request, IReadOnlyList<OverrideWindow> overrides, List<BookingViolation> violations)
    {
        var closures = overrides
            .Where(o => o.Effect == OverrideEffect.Closed && o.Range.Overlaps(request.StartUtc, request.EndUtc))
            .OrderByDescending(o => o.Scope) // most specific first: Space, Floor, Building
            .ThenBy(o => o.Range.Start);

        foreach (var closure in closures)
        {
            violations.Add(new BookingViolation(
                DixelsDomainErrorCodes.BookingSpaceClosed,
                ToSource(closure.Scope),
                BookingFormat.Data(
                    ("from", BookingFormat.DateTime(clock.ToLocal(closure.Range.Start))),
                    ("until", BookingFormat.DateTime(clock.ToLocal(closure.Range.End))),
                    ("reason", closure.ReasonDetail ?? closure.Reason.ToString()))));
        }
    }

    private static void CheckCapacity(ResolvedConstraints rules, BookingRequest request, List<BookingViolation> violations)
    {
        if (rules.Capacity is { } capacity && request.Attendees > capacity)
        {
            violations.Add(new BookingViolation(
                DixelsDomainErrorCodes.BookingOverCapacity,
                ConstraintSource.Space,
                BookingFormat.Data(("capacity", capacity), ("attendees", request.Attendees))));
        }
    }

    private static void CheckMinAttendees(ResolvedConstraints rules, BookingRequest request, List<BookingViolation> violations)
    {
        if (rules.MinAttendees is { } minAttendees && request.Attendees < minAttendees)
        {
            violations.Add(new BookingViolation(
                DixelsDomainErrorCodes.BookingBelowMinAttendees,
                ConstraintSource.Space,
                BookingFormat.Data(("minAttendees", minAttendees), ("attendees", request.Attendees))));
        }
    }

    private static void CheckMaxDuration(ResolvedConstraints rules, BookingRequest request, List<BookingViolation> violations)
    {
        var requestedMinutes = (int)(request.EndUtc - request.StartUtc).TotalMinutes;
        var max = rules.MaxDurationMinutes;

        if (requestedMinutes > max.Value)
        {
            violations.Add(new BookingViolation(
                DixelsDomainErrorCodes.BookingTooLong,
                max.Source,
                BookingFormat.Data(
                    ("maxDuration", BookingFormat.Duration(max.Value)),
                    ("requested", BookingFormat.Duration(requestedMinutes)))));
        }
    }

    // A booking must sit entirely inside ONE open interval (operating days + hours, plus
    // any special openings). Where coverage stops decides which rule is reported: if that
    // instant falls on a day the space never opens it's a Days rejection, otherwise the
    // request ran outside the hours.
    private static void CheckDaysAndHours(
        ResolvedConstraints rules,
        BuildingClock clock,
        BookingRequest request,
        IReadOnlyList<OverrideWindow> overrides,
        List<BookingViolation> violations)
    {
        var specialOpenings = overrides
            .Where(o => o.Effect == OverrideEffect.Open)
            .Select(o => o.Range);

        var open = OpenIntervals.Compute(
            rules.Days.Value,
            rules.Hours.Value,
            clock,
            clock.LocalDate(request.StartUtc),
            clock.LocalDate(request.EndUtc),
            specialOpenings);

        var uncovered = OpenIntervals.FirstUncovered(open, request.StartUtc, request.EndUtc);
        if (uncovered is null)
        {
            return;
        }

        var uncoveredLocal = clock.ToLocal(uncovered.Value);

        if (!rules.Days.Value.Contains(uncoveredLocal.DayOfWeek))
        {
            violations.Add(new BookingViolation(
                DixelsDomainErrorCodes.BookingClosedDay,
                rules.Days.Source,
                BookingFormat.Data(
                    ("day", BookingFormat.Day(uncoveredLocal.DayOfWeek)),
                    ("openDays", BookingFormat.Days(rules.Days.Value)))));
        }
        else
        {
            violations.Add(new BookingViolation(
                DixelsDomainErrorCodes.BookingOutsideHours,
                rules.Hours.Source,
                BookingFormat.Data(("hours", BookingFormat.Hours(rules.Hours.Value)))));
        }
    }

    // Counted in calendar days on the building's wall clock: with a 14-day horizon, anything
    // ending by midnight at the end of (today + 14) local is allowed — "up to 11 Oct", not
    // a cut-off at the current minute two weeks from now.
    private static void CheckHorizon(ResolvedConstraints rules, BuildingClock clock, BookingRequest request, DateTimeOffset now, List<BookingViolation> violations)
    {
        var lastDate = clock.LocalDate(now).AddDays(rules.MaxHorizonDays);
        var limit = clock.StartOfLocalDay(lastDate.AddDays(1));

        if (request.EndUtc > limit)
        {
            violations.Add(new BookingViolation(
                DixelsDomainErrorCodes.BookingBeyondHorizon,
                ConstraintSource.Building,
                BookingFormat.Data(
                    ("horizonDays", rules.MaxHorizonDays),
                    ("lastDate", BookingFormat.Date(lastDate)))));
        }
    }

    // A start in the past gets its own, clearer message rather than "needs 15 minutes'
    // notice" — the lead-time rule is what stops it either way.
    private static void CheckLeadTime(ResolvedConstraints rules, BookingRequest request, DateTimeOffset now, List<BookingViolation> violations)
    {
        if (request.StartUtc < now)
        {
            violations.Add(new BookingViolation(DixelsDomainErrorCodes.BookingStartInPast, null, BookingFormat.Data()));
            return;
        }

        if (request.StartUtc < now.AddMinutes(rules.MinLeadMinutes))
        {
            violations.Add(new BookingViolation(
                DixelsDomainErrorCodes.BookingTooSoon,
                ConstraintSource.Building,
                BookingFormat.Data(("leadMinutes", rules.MinLeadMinutes))));
        }
    }

    private static ConstraintSource ToSource(OverrideScope scope) => scope switch
    {
        OverrideScope.Building => ConstraintSource.Building,
        OverrideScope.Floor => ConstraintSource.Floor,
        OverrideScope.Space => ConstraintSource.Space,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
    };
}
