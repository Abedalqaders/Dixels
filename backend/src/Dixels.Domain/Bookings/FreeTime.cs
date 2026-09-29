using System;
using System.Collections.Generic;
using System.Linq;

namespace Dixels.Bookings;

/// <summary>
/// Answers the two follow-up questions an availability search shows next to each room:
/// "how long is it free for?" (when it's free now) and "when is it free next?" (when it
/// isn't). Pure functions over the same intervals the validator uses — open times, and the
/// blockers (closures + confirmed bookings) with the same end-exclusive overlap rule.
/// </summary>
public static class FreeTime
{
    /// <summary>
    /// The end of the free stretch that contains <c>[start, end)</c>: the first blocker that
    /// starts at or after <paramref name="end"/>, or the end of the open interval, whichever
    /// comes first. Null if the request isn't inside an open interval at all.
    /// </summary>
    public static DateTimeOffset? FreeUntil(
        IReadOnlyList<TimeRange> open,
        IEnumerable<TimeRange> blockers,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        var interval = open.FirstOrDefault(o => o.Contains(start, end));
        if (interval == default)
        {
            return null;
        }

        var limit = interval.End;
        foreach (var blocker in blockers)
        {
            if (blocker.Start >= end && blocker.Start < limit)
            {
                limit = blocker.Start;
            }
        }

        return limit;
    }

    /// <summary>
    /// The earliest start at or after <paramref name="from"/>, stepping along the slot grid,
    /// where a booking of <paramref name="length"/> fits inside one open interval without
    /// touching a blocker, finishing no later than <paramref name="latestEnd"/>. Null if
    /// nothing fits.
    /// </summary>
    public static DateTimeOffset? NextFreeStart(
        IReadOnlyList<TimeRange> open,
        IReadOnlyList<TimeRange> blockers,
        DateTimeOffset from,
        TimeSpan length,
        int slotMinutes,
        DateTimeOffset latestEnd)
    {
        var step = TimeSpan.FromMinutes(slotMinutes);

        for (var candidate = from; candidate + length <= latestEnd; candidate += step)
        {
            var candidateEnd = candidate + length;

            if (open.Any(o => o.Contains(candidate, candidateEnd))
                && !blockers.Any(b => b.Overlaps(candidate, candidateEnd)))
            {
                return candidate;
            }
        }

        return null;
    }
}
