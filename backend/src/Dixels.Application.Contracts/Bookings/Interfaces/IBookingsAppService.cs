using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Bookings;

public interface IBookingsAppService : IApplicationService
{
    /// <summary>Checks a request against every rule without reserving anything.</summary>
    Task<BookingPreviewDto> PreviewAsync(BookingRequestDto input);

    /// <summary>
    /// Books the slot, or fails with the most fundamental broken rule (HTTP 409 when the
    /// slot was taken). Retrying with the same idempotency key returns the original booking.
    /// </summary>
    Task<BookingDto> CreateAsync(CreateBookingDto input);

    /// <summary>
    /// The current user's bookings in a range of building-local days, earliest first — just
    /// enough to draw a calendar. <see cref="GetAsync"/> has the rest of any one of them.
    /// </summary>
    Task<ListResultDto<BookingSummaryDto>> GetMineAsync(GetMyBookingsInput input);

    /// <summary>One of the current user's own bookings in full (404 for anyone else's).</summary>
    Task<BookingDto> GetAsync(Guid id);

    /// <summary>
    /// Cancels one of the current user's own bookings before it starts, freeing the slot —
    /// or, for a series, this and the following ones or every upcoming one. Returns every
    /// booking cancelled.
    /// </summary>
    Task<ListResultDto<BookingDto>> CancelAsync(Guid id, CancelBookingDto input);

    /// <summary>
    /// A colleague guest accepts or declines one date they're invited to. Only a guest may (the
    /// organiser gets 403, anyone else 404); closed once the meeting starts or is cancelled.
    /// </summary>
    Task<BookingDto> RespondAsync(Guid id, RespondToInviteDto input);

    /// <summary>
    /// The same for a whole series: the series and every upcoming date, overwriting answers
    /// given to single dates. Nothing to return (204).
    /// </summary>
    Task RespondToSeriesAsync(Guid seriesId, RespondToInviteDto input);

    /// <summary>Every date of a recurring booking checked against every rule, without reserving anything.</summary>
    Task<SeriesPreviewDto> PreviewSeriesAsync(SeriesRequestDto input);

    /// <summary>Books a series' dates (minus the skipped ones) in one go — all of them, or none.</summary>
    Task<SeriesCreatedDto> CreateSeriesAsync(CreateSeriesDto input);

    /// <summary>
    /// The owner changing who's invited (and the head count) on one of their bookings that
    /// hasn't started. Not for a date of a series — that's <see cref="UpdateSeriesInviteesAsync"/>.
    /// Anyone else gets 404.
    /// </summary>
    Task<BookingDto> UpdateInviteesAsync(Guid id, UpdateInviteesDto input);

    /// <summary>The same for a whole series: its list and every upcoming date. Returns those dates.</summary>
    Task<SeriesCreatedDto> UpdateSeriesInviteesAsync(Guid seriesId, UpdateInviteesDto input);

    /// <summary>
    /// Which of these colleagues are busy at the time of one of my bookings (own booking or an
    /// accepted meeting; this booking itself doesn't count) — for Edit guests' "Busy then".
    /// Organiser only. Only the busy ones are returned.
    /// </summary>
    Task<BusyGuestsResultDto> GetBusyGuestsAsync(Guid id, BusyGuestsInput input);

    /// <summary>The same across a series' upcoming dates, with how many dates each is busy on.</summary>
    Task<BusyGuestsResultDto> GetSeriesBusyGuestsAsync(Guid seriesId, BusyGuestsInput input);
}
