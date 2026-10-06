using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Volo.Abp;

namespace Dixels.Bookings;

/// <summary>One date of a series, checked by the same rules a single booking runs.</summary>
public sealed record OccurrenceEvaluation(
    DateOnly Date,
    DateTime LocalStart,
    DateTime LocalEnd,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    IReadOnlyList<BookingViolation> Violations,
    IReadOnlyList<BookingViolation> Warnings)
{
    public bool IsValid => Violations.Count == 0;
}

/// <summary>
/// A whole series checked: problems that hit every date the same way (too many people for
/// the room, longer than it allows, off the time grid) once, in <see cref="SeriesViolations"/>,
/// and every date with whatever else is wrong with that date alone.
/// </summary>
public sealed record SeriesEvaluation(
    Space Space,
    Floor Floor,
    Building Building,
    BuildingClock LocalClock,
    ResolvedConstraints Rules,
    IReadOnlyList<BookingViolation> SeriesViolations,
    IReadOnlyList<OccurrenceEvaluation> Occurrences)
{
    public int BookableCount => SeriesViolations.Count > 0 ? 0 : Occurrences.Count(o => o.IsValid);
}

public partial class BookingManager
{
    // Same room, same people, same length every date: these fail every date or none, so
    // they're reported once for the series instead of on each of its dates.
    private static readonly HashSet<string> SeriesWideCodes = new()
    {
        DixelsDomainErrorCodes.BookingNotAligned,
        DixelsDomainErrorCodes.BookingOverCapacity,
        DixelsDomainErrorCodes.BookingBelowMinAttendees,
        DixelsDomainErrorCodes.BookingTooLong,
    };

    /// <summary>
    /// Checks every date of a recurring booking without reserving anything. The first
    /// occurrence is <paramref name="localStart"/>–<paramref name="localEnd"/>; every other
    /// date repeats that wall-clock time.
    /// </summary>
    public async Task<SeriesEvaluation> EvaluateSeriesAsync(
        Guid userId, Guid spaceId, DateTime localStart, DateTime localEnd, int attendees, RecurrenceRule rule)
    {
        var context = await LoadContextAsync(userId, spaceId, localStart, localEnd, attendees);
        return await EvaluateSeriesAsync(context, userId, localStart, localEnd, attendees, rule);
    }

    /// <summary>
    /// Books every date of a series except <paramref name="skipDates"/>, all in one
    /// transaction: if any date that should be booked no longer fits (someone took it since
    /// the preview), nothing is booked at all. A retry with the same key returns the series
    /// the first attempt created; only a new series raises <see cref="BookingSeriesConfirmedEvent"/>.
    /// </summary>
    public async Task<(BookingSeries Series, IReadOnlyList<Booking> Bookings, bool Replayed)> CreateSeriesAsync(
        Guid userId,
        Guid spaceId,
        DateTime localStart,
        DateTime localEnd,
        int attendees,
        string? title,
        RecurrenceRule rule,
        IReadOnlyCollection<DateOnly> skipDates,
        string idempotencyKey)
    {
        // Same locks as a single booking, taken once for the whole series.
        await _bookingRepository.LockSpaceAsync(spaceId);
        var context = await LoadContextAsync(userId, spaceId, localStart, localEnd, attendees);
        if (context.Building.OwnOverlapPolicy == OwnOverlapPolicy.Block)
        {
            await _bookingRepository.LockUserAsync(userId);
        }

        var existing = await _seriesRepository.FindAsync(s => s.UserId == userId && s.IdempotencyKey == idempotencyKey);
        if (existing is not null)
        {
            if (existing.SpaceId != spaceId)
            {
                throw new BusinessException(DixelsDomainErrorCodes.BookingIdempotencyKeyReused);
            }

            var made = await _bookingRepository.GetListAsync(b => b.SeriesId == existing.Id);
            return (existing, made.OrderBy(b => b.StartsAt).ToList(), true);
        }

        var evaluation = await EvaluateSeriesAsync(context, userId, localStart, localEnd, attendees, rule);
        if (evaluation.SeriesViolations.Count > 0)
        {
            throw new BookingRejectedException(evaluation.SeriesViolations);
        }

        var chosen = evaluation.Occurrences.Where(o => !skipDates.Contains(o.Date)).ToList();
        if (chosen.Count == 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.SeriesNothingToBook);
        }

        var taken = chosen.FirstOrDefault(o => !o.IsValid);
        if (taken is not null)
        {
            throw new BusinessException(DixelsDomainErrorCodes.SeriesDateUnavailable)
                .WithData("date", BookingFormat.Date(taken.Date));
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
        var series = await _seriesRepository.InsertAsync(new BookingSeries(
            GuidGenerator.Create(),
            userId,
            spaceId,
            normalizedTitle,
            attendees,
            DateOnly.FromDateTime(localStart),
            TimeOnly.FromDateTime(localStart),
            (int)(localEnd - localStart).TotalMinutes,
            rule,
            idempotencyKey), autoSave: true);

        var snapshot = _jsonSerializer.Serialize(BookingRuleSnapshot.From(evaluation.Rules));
        var bookings = new List<Booking>();
        foreach (var occurrence in chosen)
        {
            // Each row still goes through the database's no-overlap rule: a race the locks
            // didn't cover fails this insert and rolls the whole series back.
            bookings.Add(await _bookingRepository.InsertConfirmedAsync(new Booking(
                GuidGenerator.Create(),
                spaceId,
                userId,
                occurrence.StartUtc,
                occurrence.EndUtc,
                attendees,
                normalizedTitle,
                snapshot,
                $"{idempotencyKey}:{occurrence.Date:yyyyMMdd}",
                series.Id)));
        }

        await _localEventBus.PublishAsync(new BookingSeriesConfirmedEvent(series, bookings));
        return (series, bookings, false);
    }

    private async Task<SeriesEvaluation> EvaluateSeriesAsync(
        BookingContext context, Guid userId, DateTime localStart, DateTime localEnd, int attendees, RecurrenceRule rule)
    {
        var building = context.Building;
        var clock = context.LocalClock;
        var now = new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero);

        // Checked up front, so a too-far end date is one clear message rather than a list of
        // "too far ahead" dates at the end.
        var lastDate = clock.LocalDate(now).AddDays(building.MaxSeriesHorizonDays);
        if (rule.EndDate > lastDate)
        {
            throw new BusinessException(DixelsDomainErrorCodes.SeriesBeyondHorizon)
                .WithData("horizonDays", building.MaxSeriesHorizonDays)
                .WithData("lastDate", BookingFormat.Date(lastDate));
        }

        // Every date gets the first date's wall-clock time, converted to UTC for that date —
        // so across a clock change the booking stays at 10:00, not 09:00.
        var length = localEnd - localStart;
        var startTime = TimeOnly.FromDateTime(localStart);
        var slots = RecurrenceExpander.Expand(rule, DateOnly.FromDateTime(localStart), BookingConsts.MaxSeriesOccurrences)
            .Select(date =>
            {
                var start = date.ToDateTime(startTime);
                var end = start + length;
                return (Date: date, LocalStart: start, LocalEnd: end, StartUtc: clock.ToUtc(start), EndUtc: clock.ToUtc(end));
            })
            .ToList();

        // A weekly rule whose weekdays all fall after the end date expands to nothing; say so
        // instead of indexing an empty list.
        if (slots.Count == 0)
        {
            throw new BusinessException(DixelsDomainErrorCodes.SeriesNothingToBook);
        }

        var rangeStart = slots[0].StartUtc;
        var rangeEnd = slots[^1].EndUtc;

        // The series horizon stands in for the normal one; every other rule is the room's own.
        var rules = _constraintResolver.Resolve(building, context.Floor, context.Space) with
        {
            MaxHorizonDays = building.MaxSeriesHorizonDays,
        };

        // Everything each date needs, loaded once for the whole range — one query each, not one per date.
        var spaceId = context.Space.Id;
        var floorId = context.Floor.Id;
        var overrides = (await _overrideRepository.GetListAsync(o =>
                ((o.Scope == OverrideScope.Space && o.ScopeId == spaceId)
                 || (o.Scope == OverrideScope.Floor && o.ScopeId == floorId)
                 || (o.Scope == OverrideScope.Building && o.ScopeId == building.Id))
                && o.StartsAt < rangeEnd
                && o.EndsAt > rangeStart))
            .Select(OverrideWindow.From)
            .ToList();

        var roomBookings = await _bookingRepository.GetConfirmedOverlappingAsync(new[] { spaceId }, rangeStart, rangeEnd);

        var myOtherBookings = building.OwnOverlapPolicy == OwnOverlapPolicy.Allow
            ? new List<Booking>()
            : (await _bookingRepository.GetConfirmedForUserAsync(userId, rangeStart, rangeEnd)).Where(b => b.SpaceId != spaceId).ToList();
        var otherRoomNames = new Dictionary<Guid, string>();
        foreach (var id in myOtherBookings.Select(b => b.SpaceId).Distinct())
        {
            otherRoomNames[id] = await RoomNameAsync(id);
        }

        var blocks = building.OwnOverlapPolicy == OwnOverlapPolicy.Block;
        var occurrences = slots.Select(slot =>
        {
            var violations = _validator.Validate(
                    rules,
                    clock,
                    new BookingRequest(slot.StartUtc, slot.EndUtc, attendees),
                    overrides.Where(o => o.Range.Overlaps(slot.StartUtc, slot.EndUtc)).ToList(),
                    roomBookings.Any(b => b.StartsAt < slot.EndUtc && b.EndsAt > slot.StartUtc),
                    now,
                    _options.SlotMinutes)
                .ToList();
            var warnings = new List<BookingViolation>();

            var clash = myOtherBookings.FirstOrDefault(b => b.StartsAt < slot.EndUtc && b.EndsAt > slot.StartUtc);
            if (clash is not null)
            {
                var violation = OwnClashViolation(clash, otherRoomNames[clash.SpaceId], clock, blocks);
                (blocks ? violations : warnings).Add(violation);
            }

            return new OccurrenceEvaluation(slot.Date, slot.LocalStart, slot.LocalEnd, slot.StartUtc, slot.EndUtc, violations, warnings);
        }).ToList();

        // Reported once for the series, and taken off each date.
        var seriesViolations = occurrences[0].Violations.Where(v => SeriesWideCodes.Contains(v.Code)).ToList();
        if (seriesViolations.Count > 0)
        {
            occurrences = occurrences
                .Select(o => o with { Violations = o.Violations.Where(v => !SeriesWideCodes.Contains(v.Code)).ToList() })
                .ToList();
        }

        return new SeriesEvaluation(context.Space, context.Floor, building, clock, rules, seriesViolations, occurrences);
    }

    /// <summary>
    /// Cancels the owner's booking, or — for a series booking — this one and every later one,
    /// or every upcoming one. Only bookings that haven't started are ever touched; the ones
    /// under way or over stay as history. Whatever was cancelled is announced in one
    /// <see cref="BookingsCancelledEvent"/>.
    /// </summary>
    public async Task<IReadOnlyList<Booking>> CancelOwnAsync(Guid userId, Guid bookingId, string? reason, CancelScope scope)
    {
        var cancelled = await CancelOwnInScopeAsync(userId, bookingId, reason, scope);
        await _localEventBus.PublishAsync(new BookingsCancelledEvent(cancelled, byAdmin: false));
        return cancelled;
    }

    private async Task<IReadOnlyList<Booking>> CancelOwnInScopeAsync(Guid userId, Guid bookingId, string? reason, CancelScope scope)
    {
        if (scope == CancelScope.This)
        {
            return new[] { await CancelOneAsync(userId, bookingId, reason) };
        }

        var booking = await _bookingRepository.GetAsync(bookingId);
        if (booking.UserId != userId)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingNotYours);
        }

        if (booking.SeriesId is null)
        {
            return new[] { await CancelOneAsync(userId, bookingId, reason) };
        }

        var now = new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
        var seriesId = booking.SeriesId;
        var targets = (await _bookingRepository.GetListAsync(b => b.SeriesId == seriesId && b.Status == BookingStatus.Confirmed))
            .Where(b => b.StartsAt > now && (scope == CancelScope.Series || b.StartsAt >= booking.StartsAt))
            .OrderBy(b => b.StartsAt)
            .ToList();

        if (targets.Count == 0)
        {
            throw booking.Status == BookingStatus.Confirmed
                ? new BusinessException(DixelsDomainErrorCodes.BookingAlreadyStarted)
                : new BusinessException(DixelsDomainErrorCodes.BookingNotCancellable).WithData("status", booking.Status.ToString());
        }

        foreach (var target in targets)
        {
            target.Cancel(userId, now, reason, byAdmin: false);
        }

        await _bookingRepository.UpdateManyAsync(targets, autoSave: true);
        return targets;
    }

    private static BookingViolation OwnClashViolation(Booking clash, string spaceName, BuildingClock clock, bool blocks) =>
        new(
            blocks ? DixelsDomainErrorCodes.BookingOwnOverlap : DixelsDomainErrorCodes.BookingOwnOverlapWarning,
            ConstraintSource.Building,
            BookingFormat.Data(
                ("spaceName", spaceName),
                ("from", clock.ToLocal(clash.StartsAt)),
                ("until", TimeOnly.FromDateTime(clock.ToLocal(clash.EndsAt)))));
}
