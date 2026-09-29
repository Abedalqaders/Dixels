namespace Dixels.Bookings;

/// <summary>How often a recurring booking repeats — Teams' "Repeat every N day(s) / week(s) / month(s)".</summary>
public enum RecurrenceFrequency
{
    Daily,
    Weekly,
    Monthly,
}

/// <summary>For monthly repeats: the same date each month, or the same weekday position ("the 2nd Tuesday", "the last Friday").</summary>
public enum MonthlyRepeat
{
    /// <summary>"Monthly on day 29" — in a shorter month, its last day.</summary>
    OnDay,

    /// <summary>"Monthly on the 2nd Tuesday" — a 5th-week start date means "the last" one.</summary>
    OnWeekday,
}

/// <summary>Which bookings of a series a cancel applies to — Teams' three choices.</summary>
public enum CancelScope
{
    /// <summary>Only the one clicked.</summary>
    This,

    /// <summary>The one clicked and every later one in its series.</summary>
    ThisAndFollowing,

    /// <summary>Every upcoming one in the series.</summary>
    Series,
}
