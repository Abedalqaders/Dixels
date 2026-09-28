namespace Dixels;

public static class DixelsDomainErrorCodes
{
    private const string Prefix = "Dixels:SpaceManagement:";

    public const string InvalidTimezone = Prefix + "InvalidTimezone";
    public const string MaxDurationMustBePositive = Prefix + "MaxDurationMustBePositive";
    public const string MaxHorizonDaysMustBePositive = Prefix + "MaxHorizonDaysMustBePositive";
    public const string MinLeadMinutesMustNotBeNegative = Prefix + "MinLeadMinutesMustNotBeNegative";
    public const string InvalidOwnOverlapPolicy = Prefix + "InvalidOwnOverlapPolicy";
    public const string MaxSeriesHorizonTooShort = Prefix + "MaxSeriesHorizonTooShort";
    public const string TimezoneChangeWithBookings = Prefix + "TimezoneChangeWithBookings";
    public const string CapacityMustBePositive = Prefix + "CapacityMustBePositive";
    public const string CapacityBelowMinAttendees = Prefix + "CapacityBelowMinAttendees";
    public const string MinAttendeesMustBePositive = Prefix + "MinAttendeesMustBePositive";
    public const string MinAttendeesExceedsCapacity = Prefix + "MinAttendeesExceedsCapacity";
    public const string DaysNotNarrower = Prefix + "DaysNotNarrower";
    public const string HoursNotNarrower = Prefix + "HoursNotNarrower";
    public const string OverrideEndsAtMustBeAfterStartsAt = Prefix + "OverrideEndsAtMustBeAfterStartsAt";
    public const string ReasonDetailTooLong = Prefix + "ReasonDetailTooLong";
    public const string SpaceTypeNameAlreadyExists = Prefix + "SpaceTypeNameAlreadyExists";
    public const string SpaceTypeInUse = Prefix + "SpaceTypeInUse";

    public const string InvalidBuildingId = "Dixels:Users:InvalidBuildingId";

    // Booking rule violations. Each message names the rule, quotes the real limit, the level
    // that set it ({level}, read from the resolved value's provenance), and what to do next.
    private const string BookingsPrefix = "Dixels:Bookings:";

    public const string BookingNotAligned = BookingsPrefix + "NotAligned";
    public const string BookingSpaceClosed = BookingsPrefix + "SpaceClosed";
    public const string BookingOverCapacity = BookingsPrefix + "OverCapacity";
    public const string BookingBelowMinAttendees = BookingsPrefix + "BelowMinAttendees";
    public const string BookingTooLong = BookingsPrefix + "TooLong";
    public const string BookingClosedDay = BookingsPrefix + "ClosedDay";
    public const string BookingOutsideHours = BookingsPrefix + "OutsideHours";
    public const string BookingBeyondHorizon = BookingsPrefix + "BeyondHorizon";
    public const string BookingStartInPast = BookingsPrefix + "StartInPast";
    public const string BookingTooSoon = BookingsPrefix + "TooSoon";
    public const string BookingOverlap = BookingsPrefix + "Overlap";

    // Request-level errors — not rule violations, thrown before any rule is evaluated.
    public const string BookingInvalidTimeRange = BookingsPrefix + "InvalidTimeRange";
    public const string BookingAttendeesMustBePositive = BookingsPrefix + "AttendeesMustBePositive";
    public const string BookingNotAssignedToBuilding = BookingsPrefix + "NotAssignedToBuilding";
    public const string BookingIdempotencyKeyReused = BookingsPrefix + "IdempotencyKeyReused";
    public const string BookingNotCancellable = BookingsPrefix + "NotCancellable";
    public const string BookingNotYours = BookingsPrefix + "NotYours";
    public const string BookingAlreadyStarted = BookingsPrefix + "AlreadyStarted";
    public const string BookingInvalidDateRange = BookingsPrefix + "InvalidDateRange";

    // The person already has a booking at that time (building's OwnOverlapPolicy): a hard
    // rule under Block, a heads-up under Warn.
    public const string BookingOwnOverlap = BookingsPrefix + "OwnOverlap";
    public const string BookingOwnOverlapWarning = BookingsPrefix + "OwnOverlapWarning";

    // Recurring bookings: a rule that can't be expanded, and what can go wrong creating one.
    public const string SeriesInvalidRule = BookingsPrefix + "SeriesInvalidRule";
    public const string SeriesInvalidInterval = BookingsPrefix + "SeriesInvalidInterval";
    public const string SeriesNoWeekdays = BookingsPrefix + "SeriesNoWeekdays";
    public const string SeriesEndBeforeStart = BookingsPrefix + "SeriesEndBeforeStart";
    public const string SeriesTooManyOccurrences = BookingsPrefix + "SeriesTooManyOccurrences";
    public const string SeriesBeyondHorizon = BookingsPrefix + "SeriesBeyondHorizon";
    public const string SeriesNothingToBook = BookingsPrefix + "SeriesNothingToBook";
    public const string SeriesDateUnavailable = BookingsPrefix + "SeriesDateUnavailable";
}
