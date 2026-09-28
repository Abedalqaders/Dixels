using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Permissions;
using Dixels.SpaceManagement;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
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

    public BookingsAppService(
        BookingManager bookingManager,
        IRepository<Space, Guid> spaceRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Building, Guid> buildingRepository,
        BookingViolationLocalizer violationLocalizer,
        IBookingRepository bookingRepository,
        BookingAccessChecker accessChecker,
        IDataFilter dataFilter,
        IRepository<BookingSeries, Guid> seriesRepository)
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
    }

    public async Task<BookingPreviewDto> PreviewAsync(BookingRequestDto input)
    {
        var evaluation = await _bookingManager.EvaluateAsync(
            CurrentUser.GetId(), input.SpaceId, input.LocalStart, input.LocalEnd, input.Attendees);

        return new BookingPreviewDto
        {
            IsValid = evaluation.IsValid,
            Violations = evaluation.Violations.Select(_violationLocalizer.ToDto).ToList(),
            Warnings = evaluation.Warnings.Select(_violationLocalizer.ToDto).ToList(),
            StartsAt = evaluation.StartUtc,
            EndsAt = evaluation.EndUtc,
            Timezone = evaluation.Building.Timezone,
        };
    }

    [Authorize(DixelsPermissions.Bookings.Create)]
    public async Task<BookingDto> CreateAsync(CreateBookingDto input)
    {
        try
        {
            var (booking, _) = await _bookingManager.CreateAsync(
                CurrentUser.GetId(),
                input.SpaceId,
                input.LocalStart,
                input.LocalEnd,
                input.Attendees,
                input.Title,
                input.IdempotencyKey);

            return (await MapToDtosAsync(new[] { booking })).Single();
        }
        catch (BookingRejectedException ex) when (ex.Violations[0].Level is { } level)
        {
            // The domain names the level in English ("Space"); swap in the localized word so
            // the {level} placeholder in the error message reads naturally in any language.
            ex.WithData("level", _violationLocalizer.LevelName(level));
            throw;
        }
    }

    public async Task<ListResultDto<BookingDto>> GetMineAsync(GetMyBookingsInput input)
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
        var building = buildingId is null ? null : await _buildingRepository.FindAsync(buildingId.Value);
        var clock = new BuildingClock(building?.Timezone ?? "UTC");

        var now = new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero);
        var bookings = await _bookingRepository.GetCalendarForUserAsync(userId, clock.ToUtc(from), clock.ToUtc(to), now);
        return new ListResultDto<BookingDto>(await MapToDtosAsync(bookings));
    }

    [Authorize(DixelsPermissions.Bookings.Cancel)]
    public async Task<ListResultDto<BookingDto>> CancelAsync(Guid id, CancelBookingDto input)
    {
        var cancelled = await _bookingManager.CancelOwnAsync(CurrentUser.GetId(), id, input.Reason, input.Scope);
        return new ListResultDto<BookingDto>(await MapToDtosAsync(cancelled.ToList()));
    }

    public async Task<SeriesPreviewDto> PreviewSeriesAsync(SeriesRequestDto input)
    {
        var evaluation = await _bookingManager.EvaluateSeriesAsync(
            CurrentUser.GetId(), input.SpaceId, input.LocalStart, input.LocalEnd, input.Attendees, ToRule(input.Recurrence));

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
        };
    }

    [Authorize(DixelsPermissions.Bookings.Create)]
    public async Task<SeriesCreatedDto> CreateSeriesAsync(CreateSeriesDto input)
    {
        try
        {
            var (series, bookings, _) = await _bookingManager.CreateSeriesAsync(
                CurrentUser.GetId(),
                input.SpaceId,
                input.LocalStart,
                input.LocalEnd,
                input.Attendees,
                input.Title,
                ToRule(input.Recurrence),
                input.SkipDates,
                input.IdempotencyKey);

            return new SeriesCreatedDto { SeriesId = series.Id, Bookings = await MapToDtosAsync(bookings.ToList()) };
        }
        catch (BookingRejectedException ex) when (ex.Violations[0].Level is { } level)
        {
            ex.WithData("level", _violationLocalizer.LevelName(level));
            throw;
        }
    }

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
    /// Builds DTOs for a batch of bookings with one query per table (not one per booking) —
    /// shaped for lists, since "my bookings" will reuse it.
    /// </summary>
    private async Task<List<BookingDto>> MapToDtosAsync(IReadOnlyCollection<Booking> bookings)
    {
        // Deleted rooms included: a booking made before its room (or floor) was removed still
        // needs a name to show under.
        using var _ = _dataFilter.Disable<ISoftDelete>();

        var spaceIds = bookings.Select(b => b.SpaceId).Distinct().ToList();
        var spaces = (await _spaceRepository.GetListAsync(s => spaceIds.Contains(s.Id))).ToDictionary(s => s.Id);

        var floorIds = spaces.Values.Select(s => s.FloorId).Distinct().ToList();
        var floors = (await _floorRepository.GetListAsync(f => floorIds.Contains(f.Id))).ToDictionary(f => f.Id);

        var buildingIds = floors.Values.Select(f => f.BuildingId).Distinct().ToList();
        var buildings = (await _buildingRepository.GetListAsync(b => buildingIds.Contains(b.Id))).ToDictionary(b => b.Id);

        var seriesIds = bookings.Where(b => b.SeriesId != null).Select(b => b.SeriesId!.Value).Distinct().ToList();
        var seriesById = seriesIds.Count == 0
            ? new Dictionary<Guid, BookingSeries>()
            : (await _seriesRepository.GetListAsync(s => seriesIds.Contains(s.Id))).ToDictionary(s => s.Id);

        return bookings.Select(booking =>
        {
            var space = spaces[booking.SpaceId];
            var floor = floors[space.FloorId];
            var building = buildings[floor.BuildingId];
            var clock = new BuildingClock(building.Timezone);

            var dto = ObjectMapper.Map<Booking, BookingDto>(booking);
            dto.SpaceName = space.Name;
            dto.FloorName = floor.Name;
            dto.BuildingName = building.Name;
            dto.Timezone = building.Timezone;
            dto.LocalStart = clock.ToLocal(booking.StartsAt);
            dto.LocalEnd = clock.ToLocal(booking.EndsAt);
            dto.Recurrence = booking.SeriesId is { } seriesId && seriesById.TryGetValue(seriesId, out var series)
                ? ToDto(series.Rule)
                : null;
            return dto;
        }).ToList();
    }
}
