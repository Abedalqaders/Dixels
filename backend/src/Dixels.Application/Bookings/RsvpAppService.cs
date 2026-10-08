using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Localization;
using Dixels.SpaceManagement;
using Dixels.Users;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace Dixels.Bookings;

/// <summary>
/// The public answer page's two calls. Anonymous by design: whoever holds a guest's link acts as
/// that guest (see <see cref="GuestLinks"/>), and the answer goes through the same rules as in the
/// app (<see cref="BookingResponses"/>). Rate-limited per IP (DixelsRateLimitOptions.GuestLinks).
/// </summary>
[AllowAnonymous]
public class RsvpAppService : DixelsAppService, IRsvpAppService
{
    private readonly GuestLinks _guestLinks;
    private readonly BookingResponses _bookingResponses;
    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IIdentityUserRepository _userRepository;
    private readonly LocalizedNameReader _nameReader;
    private readonly UserLanguageManager _userLanguage;

    public RsvpAppService(
        GuestLinks guestLinks,
        BookingResponses bookingResponses,
        IBookingRepository bookingRepository,
        IRepository<Space, Guid> spaceRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        IIdentityUserRepository userRepository,
        LocalizedNameReader nameReader,
        UserLanguageManager userLanguage)
    {
        _guestLinks = guestLinks;
        _bookingResponses = bookingResponses;
        _bookingRepository = bookingRepository;
        _spaceRepository = spaceRepository;
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _userRepository = userRepository;
        _nameReader = nameReader;
        _userLanguage = userLanguage;
    }

    public async Task<GuestInvitationDto> LookupAsync(GuestLinkInput input)
    {
        return await DescribeAsync(await ResolveAsync(input.Token));
    }

    public async Task<GuestInvitationDto> AnswerAsync(GuestAnswerInput input)
    {
        var target = await ResolveAsync(input.Token);
        await _bookingResponses.RespondAsGuestAsync(target, input.Answer);
        return await DescribeAsync(target);
    }

    private async Task<GuestLinkTarget> ResolveAsync(string token) =>
        await _guestLinks.ResolveAsync(token)
        ?? throw new BusinessException(DixelsDomainErrorCodes.GuestLinkNotFound);

    private async Task<GuestInvitationDto> DescribeAsync(GuestLinkTarget target)
    {
        var ownerId = target.Booking?.UserId ?? target.Series!.UserId;
        var spaceId = target.Booking?.SpaceId ?? target.Series!.SpaceId;

        // The date shown: the booking itself, or a series' next date (its first when none is left).
        var now = new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
        var shown = target.Booking;
        if (shown is null)
        {
            var seriesId = target.Series!.Id;
            var dates = await _bookingRepository.GetListAsync(b => b.SeriesId == seriesId && b.Status == BookingStatus.Confirmed);
            shown = dates.Where(b => b.StartsAt > now).MinBy(b => b.StartsAt) ?? dates.MinBy(b => b.StartsAt);
        }

        var owner = await _userRepository.FindAsync(ownerId, includeDetails: false);
        var guestName = target.Row.UserId is { } guestId
            ? DisplayName(await _userRepository.FindAsync(guestId, includeDetails: false))
            : string.IsNullOrWhiteSpace(target.Row.Name) ? target.Row.Email! : target.Row.Name!;

        // The room may have been removed since (its bookings are then cancelled, so this is closed anyway).
        var space = await _spaceRepository.FindAsync(spaceId, includeDetails: true);
        var floor = space is null ? null : await _floorRepository.FindAsync(space.FloorId, includeDetails: true);
        var building = floor is null ? null : await _buildingRepository.FindAsync(floor.BuildingId, includeDetails: true);
        var clock = building is null ? null : new BuildingClock(building.Timezone);
        var translation = building is null ? null : await _nameReader.ShownTranslationAsync(building);

        return new GuestInvitationDto
        {
            GuestName = guestName,
            InvitedBy = DisplayName(owner),
            Title = target.Booking?.Title ?? target.Series!.Title,
            SpaceName = space is null ? string.Empty : await _nameReader.ShownAsync(space),
            FloorName = floor is null ? string.Empty : await _nameReader.ShownAsync(floor),
            BuildingName = translation?.Name ?? string.Empty,
            Address = translation?.Address,
            LocalStart = shown is null || clock is null ? default : clock.ToLocal(shown.StartsAt),
            LocalEnd = shown is null || clock is null ? default : clock.ToLocal(shown.EndsAt),
            Recurrence = target.Series is { } series ? ToDto(series.Rule) : null,
            MyResponse = target.Row.ResponseStatus,
            IsOpen = await _bookingResponses.IsOpenAsync(target),
            Language = await _userLanguage.GetAsync(ownerId),
        };
    }

    private static string DisplayName(IdentityUser? user)
    {
        if (user is null)
        {
            return string.Empty;
        }

        var name = $"{user.Name} {user.Surname}".Trim();
        return name.Length > 0 ? name : user.UserName;
    }

    private static RecurrenceDto ToDto(RecurrenceRule rule) => new()
    {
        Frequency = rule.Frequency,
        Interval = rule.Interval,
        Weekdays = rule.Weekdays.Select(d => (int)d).ToArray(),
        MonthlyRepeat = rule.MonthlyRepeat,
        EndDate = rule.EndDate,
    };
}
