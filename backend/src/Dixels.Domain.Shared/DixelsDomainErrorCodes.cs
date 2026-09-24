namespace Dixels;

public static class DixelsDomainErrorCodes
{
    private const string Prefix = "Dixels:SpaceManagement:";
    private const string EmployeesPrefix = "Dixels:Employees:";

    public const string InvalidTimezone = Prefix + "InvalidTimezone";
    public const string MaxDurationMustBePositive = Prefix + "MaxDurationMustBePositive";
    public const string MaxHorizonDaysMustBePositive = Prefix + "MaxHorizonDaysMustBePositive";
    public const string MinLeadMinutesMustNotBeNegative = Prefix + "MinLeadMinutesMustNotBeNegative";
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

    public const string UserIsNotAnEmployee = EmployeesPrefix + "UserIsNotAnEmployee";
}
