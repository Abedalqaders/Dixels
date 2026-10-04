using Dixels.Localization;
using Dixels.SpaceManagement;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;

namespace Dixels.Bookings;

/// <summary>
/// Turns a domain <see cref="BookingViolation"/> into the localized text the UI shows — the
/// full sentence (<c>{code}</c>) and the few-words version (<c>{code}:Short</c>), with the
/// violation's data worded into the placeholders (<see cref="BookingValueFormatter"/>) and
/// the level named in the user's language
/// (<c>Enum:ConstraintSource.Space</c>). Shared by preview, create and availability search
/// so a rule reads identically wherever it's reported.
/// </summary>
public class BookingViolationLocalizer : ITransientDependency
{
    private readonly IStringLocalizer<DixelsResource> _localizer;
    private readonly BookingValueFormatter _formatter;

    public BookingViolationLocalizer(IStringLocalizer<DixelsResource> localizer, BookingValueFormatter formatter)
    {
        _localizer = localizer;
        _formatter = formatter;
    }

    public BookingViolationDto ToDto(BookingViolation violation)
    {
        return new BookingViolationDto
        {
            Code = violation.Code,
            Level = violation.Level?.ToString(),
            Message = Fill(_localizer[violation.Code].Value, violation),
            ShortMessage = Fill(_localizer[violation.Code + ":Short"].Value, violation),
        };
    }

    public string LevelName(ConstraintSource level) => _localizer["Enum:ConstraintSource." + level].Value;

    private string Fill(string template, BookingViolation violation)
    {
        foreach (var (key, value) in violation.Data)
        {
            template = template.Replace("{" + key + "}", _formatter.Format(value));
        }

        return violation.Level is { } level
            ? template.Replace("{level}", LevelName(level))
            : template;
    }
}
