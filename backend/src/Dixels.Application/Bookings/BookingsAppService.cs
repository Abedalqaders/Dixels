using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Localization;
using Dixels.Permissions;
using Dixels.SpaceManagement;
using Dixels.Users;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Bookings;

[Authorize(DixelsPermissions.Bookings.Default)]
public class BookingsAppService : DixelsAppService, IBookingsAppService
{
    private readonly BookingManager _bookingManager;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly BookingViolationLocalizer _violationLocalizer;
    private readonly IBookingRepository _bookingRepository;
    private readonly BookingAccessChecker _accessChecker;
    private readonly IDataFilter _dataFilter;
    private readonly IRepository<BookingSeries, Guid> _seriesRepository;
    private readonly LocalizedNameReader _nameReader;
    private readonly IIdentityUserRepository _userRepository;
    private readonly ConstraintResolver _constraintResolver;
    private readonly BookingAccess _bookingAccess;
    private readonly BookingResponses _bookingResponses;
    private readonly BookingBusyFinder _busyFinder;

    public BookingsAppService(
        BookingManager bookingManager,
        IRepository<Space, Guid> spaceRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        BookingViolationLocalizer violationLocalizer,
        IBookingRepository bookingRepository,
        BookingAccessChecker accessChecker,
        IDataFilter dataFilter,
        IRepository<BookingSeries, Guid> seriesRepository,
        LocalizedNameReader nameReader,
        IIdentityUserRepository userRepository,
        ConstraintResolver constraintResolver,
        BookingAccess bookingAccess,
        BookingResponses bookingResponses,
        BookingBusyFinder busyFinder)
    {
        _bookingManager = bookingManager;
        _spaceRepository = spaceRepository;
        _floorRepository = floorRepository;
        _buildingRepository = buildingRepository;
        _violationLocalizer = violationLocalizer;
        _bookingRepository = bookingRepository;
        _accessChecker = accessChecker;
        _dataFilter = dataFilter;
        _seriesRepository = seriesRepository;
        _nameReader = nameReader;
        _userRepository = userRepository;
        _constraintResolver = constraintResolver;
        _bookingAccess = bookingAccess;
        _bookingResponses = bookingResponses;
        _busyFinder = busyFinder;
    }

    public async Task<BookingPreviewDto> PreviewAsync(BookingRequestDto input)
    {
        var evaluation = await _bookingManager.EvaluateAsync(
            CurrentUser.GetId(), input.SpaceId, input.LocalStart, input.LocalEnd, input.Attendees, ToInvitees(input.Invitees));

        return new BookingPreviewDto
        {
            IsValid = evaluation.IsValid,
            Violations = evaluation.Violations.Select(_violationLocalizer.ToDto).ToList(),
            Warnings = evaluation.Warnings.Select(_violationLocalizer.ToDto).ToList(),
            StartsAt = evaluation.StartUtc,
            EndsAt = evaluation.EndUtc,
            Timezone = evaluation.Building.Timezone,
            Invitees = evaluation.Invitees.Select(i => ToOwnersView(i, evaluation.Busy, evaluation.LocalClock)).ToList(),
        };
    }

    [Authorize(DixelsPermissions.Bookings.Create)]
    public async Task<BookingDto> CreateAsync(CreateBookingDto input)
    {
        try
        {
            var (booking, _, place) = await _bookingManager.CreateAsync(
                CurrentUser.GetId(),
                input.SpaceId,
                input.LocalStart,
                input.LocalEnd,
                input.Attendees,
                ToInvitees(input.Invitees),
                input.Title,
                input.IdempotencyKey);

            return (await MapToDtosAsync(new[] { booking }, await PlacesOfAsync(place))).Single();
        }
        catch (BookingRejectedException ex) when (ex.Violations[0].Level is { } level)
        {
            // The domain names the level in English ("Space"); swap in the localized word so
            // the {level} placeholder in the error message reads naturally in any language.
            ex.WithData("level", _violationLocalizer.LevelName(level));
            throw;
        }
    }

    public async Task<ListResultDto<BookingSummaryDto>> GetMineAsync(GetMyBookingsInput input)
    {
        var from = input.From.Date;
        var to = input.To.Date;
        if (to <= from || (to - from).TotalDays > GetMyBookingsInput.MaxDays)
        {
            throw new BusinessException(DixelsDomainErrorCodes.BookingInvalidDateRange)
                .WithData("maxDays", GetMyBookingsInput.MaxDays);
        }

        // The days are the employee's building's days. Someone not assigned anywhere (any
        // more) still sees what they booked — on UTC days, the one neutral choice left.
        var userId = CurrentUser.GetId();
        var buildingId = await _accessChecker.FindBookableBuildingIdAsync(userId);
        string? timezone = null;
        if (buildingId is not null)
        {
            // Deleted included: an employee whose building was removed still sees their
            // (cancelled) bookings on that building's clock. Only its timezone: the calendar
            // shows no building name, so the building and its names aren't loaded.
            using (_dataFilter.Disable<ISoftDelete>())
            {
                var buildings = await _buildingRepository.GetQueryableAsync();
                timezone = await AsyncExecuter.FirstOrDefaultAsync(
                    buildings.Where(b => b.Id == buildingId.Value).Select(b => b.Timezone));
            }
        }
        var clock = new BuildingClock(timezone ?? "UTC");

        // A building that's gone altogether (not found) filters nothing, as before.
        var now = new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
        var bookings = await _bookingRepository.GetCalendarForUserAsync(
            userId, clock.ToUtc(from), clock.ToUtc(to), now, timezone is null ? null : buildingId);
        return new ListResultDto<BookingSummaryDto>(await MapToSummariesAsync(bookings, userId));
    }

    public async Task<BookingDto> GetAsync(Guid id)
    {
        var booking = await _bookingRepository.GetAsync(id);
        // The organiser and a colleague guest may read it (MapToDtosAsync hides the other
        // guests' emails from a guest); to anyone else it doesn't exist.
        await _bookingAccess.EnsureAsync(booking, CurrentUser.GetId(), BookingRole.Owner, BookingRole.Guest);

        var dto = (await MapToDtosAsync(new[] { booking })).Single();

        // A guest about to answer: am I already taken then? Only while answers are open, and
        // never counting this booking itself.
        var me = CurrentUser.GetId();
        if (!dto.IsOwner && booking.Status == BookingStatus.Confirmed && booking.StartsAt > new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero))
        {
            var busy = await _busyFinder.FindBusyAsync(
                new[] { me }, new[] { new TimeRange(booking.StartsAt, booking.EndsAt) }, new[] { booking.Id });
            if (busy.TryGetValue(me, out var mine))
            {
                dto.MyBusy = ToTimes(mine, new BuildingClock(dto.Timezone));
            }
        }

        return dto;
    }

    [Authorize(DixelsPermissions.Bookings.Cancel)]
    public async Task<ListResultDto<BookingDto>> CancelAsync(Guid id, CancelBookingDto input)
    {
        // Only the organiser cancels: a guest is told so (403), anyone else that it doesn't exist.
        var booking = await _bookingRepository.GetAsync(id);
        await _bookingAccess.EnsureAsync(booking, CurrentUser.GetId(), DixelsDomainErrorCodes.BookingOnlyOrganiserCancels, BookingRole.Owner);

        var cancelled = await _bookingManager.CancelOwnAsync(CurrentUser.GetId(), id, input.Reason, input.Scope);
        return new ListResultDto<BookingDto>(await MapToDtosAsync(cancelled.ToList()));
    }

    public async Task<BookingDto> RespondAsync(Guid id, RespondToInviteDto input)
    {
        var booking = await _bookingResponses.RespondAsync(id, CurrentUser.GetId(), input.Status);
        return (await MapToDtosAsync(new[] { booking })).Single();
    }

    public Task RespondToSeriesAsync(Guid seriesId, RespondToInviteDto input) =>
        _bookingResponses.RespondToSeriesAsync(seriesId, CurrentUser.GetId(), input.Status);

    public async Task<SeriesPreviewDto> PreviewSeriesAsync(SeriesRequestDto input)
    {
        var evaluation = await _bookingManager.EvaluateSeriesAsync(
            CurrentUser.GetId(), input.SpaceId, input.LocalStart, input.LocalEnd, input.Attendees, ToInvitees(input.Invitees), ToRule(input.Recurrence));

        var seriesWide = evaluation.SeriesViolations.Count > 0;
        return new SeriesPreviewDto
        {
            SeriesViolations = evaluation.SeriesViolations.Select(_violationLocalizer.ToDto).ToList(),
            Occurrences = evaluation.Occurrences.Select(o => new OccurrencePreviewDto
            {
                Date = o.Date,
                LocalStart = o.LocalStart,
                LocalEnd = o.LocalEnd,
                IsValid = !seriesWide && o.IsValid,
                Violations = o.Violations.Select(_violationLocalizer.ToDto).ToList(),
                Warnings = o.Warnings.Select(_violationLocalizer.ToDto).ToList(),
            }).ToList(),
            BookableCount = evaluation.BookableCount,
            Timezone = evaluation.Building.Timezone,
            Invitees = evaluation.Invitees.Select(i => ToOwnersView(i, evaluation.Busy, evaluation.LocalClock)).ToList(),
        };
    }

    [Authorize(DixelsPermissions.Bookings.Create)]
    public async Task<SeriesCreatedDto> CreateSeriesAsync(CreateSeriesDto input)
    {
        try
        {
            var (series, bookings, _, place) = await _bookingManager.CreateSeriesAsync(
                CurrentUser.GetId(),
                input.SpaceId,
                input.LocalStart,
                input.LocalEnd,
                input.Attendees,
                ToInvitees(input.Invitees),
                input.Title,
                ToRule(input.Recurrence),
                input.SkipDates,
                input.IdempotencyKey);

            return new SeriesCreatedDto { SeriesId = series.Id, Bookings = await MapToDtosAsync(bookings.ToList(), await PlacesOfAsync(place)) };
        }
        catch (BookingRejectedException ex) when (ex.Violations[0].Level is { } level)
        {
            ex.WithData("level", _violationLocalizer.LevelName(level));
            throw;
        }
    }

    [Authorize(DixelsPermissions.Bookings.Create)]
    public async Task<BookingDto> UpdateInviteesAsync(Guid id, UpdateInviteesDto input)
    {
        try
        {
            var (booking, place) = await _bookingManager.ChangeInviteesAsync(
                CurrentUser.GetId(), id, input.Attendees, ToInvitees(input.Invitees));
            return (await MapToDtosAsync(new[] { booking }, await PlacesOfAsync(place))).Single();
        }
        catch (BookingRejectedException ex) when (ex.Violations[0].Level is { } level)
        {
            ex.WithData("level", _violationLocalizer.LevelName(level));
            throw;
        }
    }

    [Authorize(DixelsPermissions.Bookings.Create)]
    public async Task<SeriesCreatedDto> UpdateSeriesInviteesAsync(Guid seriesId, UpdateInviteesDto input)
    {
        try
        {
            var (series, bookings, place) = await _bookingManager.ChangeSeriesInviteesAsync(
                CurrentUser.GetId(), seriesId, input.Attendees, ToInvitees(input.Invitees));
            return new SeriesCreatedDto { SeriesId = series.Id, Bookings = await MapToDtosAsync(bookings.ToList(), await PlacesOfAsync(place)) };
        }
        catch (BookingRejectedException ex) when (ex.Violations[0].Level is { } level)
        {
            ex.WithData("level", _violationLocalizer.LevelName(level));
            throw;
        }
    }

    private static List<Invitee> ToInvitees(IEnumerable<InviteeDto>? dtos) =>
        dtos?.Select(d => new Invitee(d.UserId, d.Email, d.Name)).ToList() ?? new List<Invitee>();

    /// <summary>A checked invitee as the booker sees them: with their email (only the owner sees guests' emails).</summary>
    public async Task<BusyGuestsResultDto> GetBusyGuestsAsync(Guid id, BusyGuestsInput input) =>
        ToBusyGuests(await _bookingManager.FindBusyGuestsAsync(CurrentUser.GetId(), id, input.UserIds));

    public async Task<BusyGuestsResultDto> GetSeriesBusyGuestsAsync(Guid seriesId, BusyGuestsInput input) =>
        ToBusyGuests(await _bookingManager.FindBusySeriesGuestsAsync(CurrentUser.GetId(), seriesId, input.UserIds));

    private static BusyGuestsResultDto ToBusyGuests(BusyGuests result) => new()
    {
        Dates = result.Dates,
        Items = result.Busy
            .Select(b => new BusyGuestDto { UserId = b.Key, BusyDates = b.Value.Dates, Times = ToTimes(b.Value, result.Clock) })
            .ToList(),
    };

    private static List<BusyTimeDto> ToTimes(PersonBusy busy, BuildingClock clock) =>
        busy.Times.Select(t => new BusyTimeDto { LocalStart = clock.ToLocal(t.Start), LocalEnd = clock.ToLocal(t.End) }).ToList();

    private static BookingInviteeDto ToOwnersView(Invitee invitee, IReadOnlyDictionary<Guid, PersonBusy>? busy, BuildingClock clock)
    {
        var dto = ToOwnersView(invitee);
        if (invitee.UserId is { } id && busy is not null && busy.TryGetValue(id, out var theirs))
        {
            dto.IsBusy = true;
            dto.BusyDates = theirs.Dates;
            dto.BusyTimes = ToTimes(theirs, clock);
        }

        return dto;
    }

    private static BookingInviteeDto ToOwnersView(Invitee invitee) => new()
    {
        UserId = invitee.UserId,
        Name = invitee.Name ?? string.Empty,
        Email = invitee.Email ?? string.Empty,
        IsExternal = invitee.IsExternal,
    };

    private static RecurrenceRule ToRule(RecurrenceDto dto) =>
        new(dto.Frequency, dto.Interval, dto.Weekdays.Select(d => (DayOfWeek)d), dto.MonthlyRepeat, dto.EndDate);

    private static RecurrenceDto ToDto(RecurrenceRule rule) => new()
    {
        Frequency = rule.Frequency,
        Interval = rule.Interval,
        Weekdays = rule.Weekdays.Select(d => (int)d).ToArray(),
        MonthlyRepeat = rule.MonthlyRepeat,
        EndDate = rule.EndDate,
    };

    /// <summary>
    /// The rooms, floors and buildings a batch of bookings sit in — one query per table, not
    /// one per booking — with each one's name in the reader's language, by id.
    /// </summary>
    private sealed record Places(
        Dictionary<Guid, Space> Spaces,
        Dictionary<Guid, Floor> Floors,
        Dictionary<Guid, Building> Buildings,
        Dictionary<Guid, string> Names)
    {
        public (Space Space, Floor Floor, Building Building) Of(Booking booking)
        {
            var space = Spaces[booking.SpaceId];
            var floor = Floors[space.FloorId];
            return (space, floor, Buildings[floor.BuildingId]);
        }
    }

    private async Task<Places> LoadPlacesAsync(IReadOnlyCollection<Booking> bookings)
    {
        // Deleted rooms included: a booking made before its room (or floor) was removed still
        // needs a name to show under.
        using var _ = _dataFilter.Disable<ISoftDelete>();

        var spaceIds = bookings.Select(b => b.SpaceId).Distinct().ToList();
        // With details: their names.
        var spaces = (await _spaceRepository.GetListAsync(s => spaceIds.Contains(s.Id), includeDetails: true)).ToDictionary(s => s.Id);

        var floorIds = spaces.Values.Select(s => s.FloorId).Distinct().ToList();
        var floors = (await _floorRepository.GetListAsync(f => floorIds.Contains(f.Id), includeDetails: true)).ToDictionary(f => f.Id);

        var buildingIds = floors.Values.Select(f => f.BuildingId).Distinct().ToList();
        var buildings = (await _buildingRepository.GetListAsync(b => buildingIds.Contains(b.Id), includeDetails: true)).ToDictionary(b => b.Id);

        // Ids are unique across the three tables, so one lookup serves them all.
        var names = (await _nameReader.ShownAsync<Space, SpaceTranslation>(spaces.Values))
            .Concat(await _nameReader.ShownAsync<Floor, FloorTranslation>(floors.Values))
            .Concat(await _nameReader.ShownAsync<Building, BuildingTranslation>(buildings.Values))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        return new Places(spaces, floors, buildings, names);
    }

    /// <summary>The one room a create already loaded (with its names), as <see cref="Places"/>: nothing read again.</summary>
    private async Task<Places> PlacesOfAsync(BookingPlace place)
    {
        var names = new Dictionary<Guid, string>
        {
            [place.Space.Id] = await _nameReader.ShownAsync(place.Space),
            [place.Floor.Id] = await _nameReader.ShownAsync(place.Floor),
            [place.Building.Id] = await _nameReader.ShownAsync(place.Building),
        };

        return new Places(
            new Dictionary<Guid, Space> { [place.Space.Id] = place.Space },
            new Dictionary<Guid, Floor> { [place.Floor.Id] = place.Floor },
            new Dictionary<Guid, Building> { [place.Building.Id] = place.Building },
            names);
    }

    /// <summary>
    /// The calendar's light rows: no floor/building names, no series rule — just what's drawn.
    /// So only the rooms are loaded (for their names); each floor gives just its building's
    /// timezone, in one small query, instead of whole floors and buildings with their names.
    /// </summary>
    private async Task<List<BookingSummaryDto>> MapToSummariesAsync(IReadOnlyCollection<Booking> bookings, Guid me)
    {
        if (bookings.Count == 0)
        {
            return new List<BookingSummaryDto>();
        }

        // Deleted ones included, as in LoadPlacesAsync: a booking in a removed room or building
        // still shows (struck through), under its name and on its building's clock.
        using var _ = _dataFilter.Disable<ISoftDelete>();

        var spaceIds = bookings.Select(b => b.SpaceId).Distinct().ToList();
        var spaces = (await _spaceRepository.GetListAsync(s => spaceIds.Contains(s.Id), includeDetails: true)).ToDictionary(s => s.Id);
        var names = await _nameReader.ShownAsync<Space, SpaceTranslation>(spaces.Values);

        // My answers to the invites among them — one small query, and only when there are invites.
        var responses = await _bookingRepository.GetResponsesAsync(bookings.Where(b => b.UserId != me).Select(b => b.Id).ToList(), me);

        var floorIds = spaces.Values.Select(s => s.FloorId).Distinct().ToList();
        var floors = await _floorRepository.GetQueryableAsync();
        var buildings = await _buildingRepository.GetQueryableAsync();
        var timezoneByFloor = (await AsyncExecuter.ToListAsync(
                from f in floors
                join b in buildings on f.BuildingId equals b.Id
                where floorIds.Contains(f.Id)
                select new { FloorId = f.Id, b.Timezone }))
            .ToDictionary(x => x.FloorId, x => x.Timezone);

        return bookings.Select(booking =>
        {
            var space = spaces[booking.SpaceId];
            var clock = new BuildingClock(timezoneByFloor[space.FloorId]);
            return new BookingSummaryDto
            {
                Id = booking.Id,
                Title = booking.Title,
                LocalStart = clock.ToLocal(booking.StartsAt),
                LocalEnd = clock.ToLocal(booking.EndsAt),
                SpaceName = names[space.Id],
                Status = booking.Status.ToString(),
                SeriesId = booking.SeriesId,
                IsInvited = booking.UserId != me,
                MyResponse = booking.UserId == me ? null : responses.GetValueOrDefault(booking.Id),
            };
        }).ToList();
    }

    private static BookingInviteeDto ToInviteeDto(BookingAttendee invitee, IReadOnlyDictionary<Guid, IdentityUser> users, bool showEmail)
    {
        var colleague = invitee.UserId is { } id ? users.GetValueOrDefault(id) : null;
        return new BookingInviteeDto
        {
            UserId = invitee.UserId,
            Name = colleague?.DisplayName() ?? invitee.Name ?? string.Empty,
            Email = showEmail ? colleague?.Email ?? invitee.Email ?? string.Empty : string.Empty,
            IsExternal = invitee.UserId is null,
            ResponseStatus = invitee.ResponseStatus,
        };
    }

    /// <summary>
    /// Builds full DTOs for a batch of bookings with one query per table (not one per booking),
    /// or none for the places when the caller already has them (a create).
    /// </summary>
    private async Task<List<BookingDto>> MapToDtosAsync(IReadOnlyCollection<Booking> bookings, Places? knownPlaces = null)
    {
        var places = knownPlaces ?? await LoadPlacesAsync(bookings);

        var seriesIds = bookings.Where(b => b.SeriesId != null).Select(b => b.SeriesId!.Value).Distinct().ToList();
        var seriesById = seriesIds.Count == 0
            ? new Dictionary<Guid, BookingSeries>()
            : (await _seriesRepository.GetListAsync(s => seriesIds.Contains(s.Id))).ToDictionary(s => s.Id);

        // The owners and the colleagues invited, in one query, for their current names (and
        // emails). Deleted users included: a booking still names who made it.
        var me = CurrentUser.GetId();
        var userIds = bookings.Select(b => b.UserId)
            .Concat(bookings.SelectMany(b => b.Invitees).Where(i => i.UserId != null).Select(i => i.UserId!.Value))
            .Distinct()
            .ToList();
        Dictionary<Guid, IdentityUser> users;
        using (_dataFilter.Disable<ISoftDelete>())
        {
            users = (await _userRepository.GetListByIdsAsync(userIds)).ToDictionary(u => u.Id);
        }

        return bookings.Select(booking =>
        {
            var (space, floor, building) = places.Of(booking);
            var clock = new BuildingClock(building.Timezone);

            var dto = ObjectMapper.Map<Booking, BookingDto>(booking);
            dto.SpaceName = places.Names[space.Id];
            dto.FloorName = places.Names[floor.Id];
            dto.BuildingName = places.Names[building.Id];
            dto.Timezone = building.Timezone;
            dto.LocalStart = clock.ToLocal(booking.StartsAt);
            dto.LocalEnd = clock.ToLocal(booking.EndsAt);
            dto.Recurrence = booking.SeriesId is { } seriesId && seriesById.TryGetValue(seriesId, out var series)
                ? ToDto(series.Rule)
                : null;
            var rules = _constraintResolver.Resolve(building, floor, space);
            dto.Capacity = rules.Capacity;
            dto.MinAttendees = rules.MinAttendees;
            dto.IsOwner = booking.UserId == me;
            dto.OwnerName = users.TryGetValue(booking.UserId, out var owner) ? owner.DisplayName() : string.Empty;
            dto.Invitees = booking.Invitees.Select(i => ToInviteeDto(i, users, dto.IsOwner)).ToList();
            dto.MyResponse = dto.IsOwner ? null : booking.Invitees.FirstOrDefault(i => i.UserId == me)?.ResponseStatus;
            return dto;
        }).ToList();
    }
}
