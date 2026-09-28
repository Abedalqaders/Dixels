using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Localization;
using Dixels.SpaceManagement;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace Dixels.Bookings;

/// <summary>
/// The application side of <see cref="BookingImpactChecker"/>, shared by every admin action
/// that can leave upcoming bookings behind (a constraint save at any level, a new closure,
/// a delete): turns the affected bookings into what the admin reads before confirming, and
/// cancels them with a reason the employee will see on their calendar.
/// </summary>
public class BookingImpactService : ITransientDependency
{
    private readonly BookingImpactChecker _checker;
    private readonly IIdentityUserRepository _userRepository;
    private readonly BookingViolationLocalizer _violationLocalizer;
    private readonly IStringLocalizer<DixelsResource> _localizer;

    public BookingImpactService(
        BookingImpactChecker checker,
        IIdentityUserRepository userRepository,
        BookingViolationLocalizer violationLocalizer,
        IStringLocalizer<DixelsResource> localizer)
    {
        _checker = checker;
        _userRepository = userRepository;
        _violationLocalizer = violationLocalizer;
        _localizer = localizer;
    }

    public BookingImpactChecker Checker => _checker;

    /// <summary>The bookings as the admin reads them; <paramref name="fixedReason"/> replaces the per-rule reasons (a delete).</summary>
    public async Task<BookingImpactDto> DescribeAsync(Building building, IReadOnlyList<BookingImpact> impacts, string? fixedReason = null)
    {
        var clock = new BuildingClock(building.Timezone);
        var userIds = impacts.Select(i => i.Booking.UserId).Distinct().ToList();
        var users = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _userRepository.GetListByIdsAsync(userIds)).ToDictionary(
                u => u.Id,
                u => string.IsNullOrWhiteSpace(u.Name) ? u.UserName : $"{u.Name} {u.Surname}".Trim());

        return new BookingImpactDto
        {
            Count = impacts.Count,
            Bookings = impacts.Select(i => new AffectedBookingDto
            {
                BookingId = i.Booking.Id,
                Title = i.Booking.Title,
                BookedBy = users.GetValueOrDefault(i.Booking.UserId, "Someone"),
                SpaceName = i.Space.Name,
                FloorName = i.Floor.Name,
                LocalStart = clock.ToLocal(i.Booking.StartsAt),
                LocalEnd = clock.ToLocal(i.Booking.EndsAt),
                Reasons = fixedReason is not null
                    ? new List<string> { fixedReason }
                    : i.Violations.Select(v => _violationLocalizer.ToDto(v).ShortMessage).Distinct().ToList(),
            }).ToList(),
        };
    }

    /// <summary>Cancels bookings a rule change broke: "Rules changed: Open 09:00–17:00 only".</summary>
    public Task CancelForRuleChangeAsync(IReadOnlyList<BookingImpact> impacts, Guid adminId)
    {
        var reasons = impacts.ToDictionary(
            i => i.Booking.Id,
            i => _localizer["Dixels:Bookings:CancelReason:RulesChanged", _violationLocalizer.ToDto(i.Violations[0]).ShortMessage].Value);
        return _checker.CancelAsAdminAsync(impacts.Select(i => i.Booking).ToList(), adminId, b => reasons[b.Id]);
    }

    /// <summary>Cancels bookings with one reason for all (a closure, a removed room).</summary>
    public Task CancelAllAsync(IReadOnlyList<BookingImpact> impacts, Guid adminId, string reason) =>
        _checker.CancelAsAdminAsync(impacts.Select(i => i.Booking).ToList(), adminId, _ => reason);

    public string Text(string key, params object[] args) => _localizer[key, args].Value;

    /// <summary>Every upcoming booking on these rooms, as impacts with no rule broken — for a delete.</summary>
    public async Task<IReadOnlyList<BookingImpact>> UpcomingAsync(IReadOnlyList<(Space Space, Floor Floor)> rooms)
    {
        var byId = rooms.ToDictionary(r => r.Space.Id);
        var bookings = await _checker.FindUpcomingAsync(byId.Keys.ToList());
        return bookings
            .Select(b => new BookingImpact(b, byId[b.SpaceId].Space, byId[b.SpaceId].Floor, Array.Empty<BookingViolation>()))
            .ToList();
    }
}
