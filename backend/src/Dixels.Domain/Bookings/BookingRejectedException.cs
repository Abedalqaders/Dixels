using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Dixels.Bookings;

/// <summary>
/// A booking that broke one or more rules. Its code and message are the most fundamental
/// violation (the first one); <see cref="Violations"/> carries all of them so a caller
/// can show the full list.
/// </summary>
public class BookingRejectedException : BusinessException
{
    public IReadOnlyList<BookingViolation> Violations { get; }

    public BookingRejectedException(IReadOnlyList<BookingViolation> violations)
        : base(violations.First().Code)
    {
        Violations = violations;

        var first = violations[0];
        foreach (var (key, value) in first.Data)
        {
            WithData(key, value);
        }

        if (first.Level is { } level)
        {
            WithData("level", level.ToString());
        }
    }
}
