using System;
using System.Linq;
using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.SpaceManagement;

/// <summary>
/// Converts between the DTO-primitive shapes (int[] days, {isOpen24Hours,open,close} hours)
/// and the real OperatingDays/OperatingWindow value objects. Kept out of the Mapperly mapper
/// deliberately: choosing OperatingWindow.FullDay vs. the regular constructor based on
/// isOpen24Hours is a real decision, not a dumb property copy, and Application.Contracts
/// can't reference these value-object types anyway (they live in Dixels.Domain). Shared
/// across Building/Floor/Space, which all use the same wire shapes.
/// </summary>
internal static class ConstraintDtoConversions
{
    public static OperatingDays ToOperatingDays(int[] days)
    {
        return OperatingDays.FromDayOfWeeks(days.Select(d => (DayOfWeek)d));
    }

    public static int[] ToDayArray(OperatingDays days)
    {
        return days.ToDayOfWeeks().Select(d => (int)d).ToArray();
    }

    public static OperatingWindow ToOperatingWindow(OperatingWindowDto dto)
    {
        return dto.IsOpen24Hours
            ? OperatingWindow.FullDay
            : OperatingWindow.Create(TimeOnly.Parse(dto.Open), TimeOnly.Parse(dto.Close));
    }

    public static OperatingWindowDto ToWindowDto(OperatingWindow window)
    {
        return new OperatingWindowDto
        {
            IsOpen24Hours = window.IsOpen24Hours,
            Open = window.Open.ToString("HH:mm"),
            Close = window.Close.ToString("HH:mm"),
        };
    }

    // Nullable-aware variants for Floor/Space, where null genuinely means "inherit" rather
    // than an absent value to reject.
    public static OperatingDays? ToOperatingDaysOrNull(int[]? days) => days is null ? null : ToOperatingDays(days);

    public static int[]? ToDayArrayOrNull(OperatingDays? days) => days is null ? null : ToDayArray(days);

    public static OperatingWindow? ToOperatingWindowOrNull(OperatingWindowDto? dto) => dto is null ? null : ToOperatingWindow(dto);

    public static OperatingWindowDto? ToWindowDtoOrNull(OperatingWindow? window) => window is null ? null : ToWindowDto(window);
}
