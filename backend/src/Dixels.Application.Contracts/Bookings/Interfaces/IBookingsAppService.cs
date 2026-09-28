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

    /// <summary>The current user's confirmed bookings in a range of building-local days, earliest first.</summary>
    Task<ListResultDto<BookingDto>> GetMineAsync(GetMyBookingsInput input);

    /// <summary>
    /// Cancels one of the current user's own bookings before it starts, freeing the slot —
    /// or, for a series, this and the following ones or every upcoming one. Returns every
    /// booking cancelled.
    /// </summary>
    Task<ListResultDto<BookingDto>> CancelAsync(Guid id, CancelBookingDto input);

    /// <summary>Every date of a recurring booking checked against every rule, without reserving anything.</summary>
    Task<SeriesPreviewDto> PreviewSeriesAsync(SeriesRequestDto input);

    /// <summary>Books a series' dates (minus the skipped ones) in one go — all of them, or none.</summary>
    Task<SeriesCreatedDto> CreateSeriesAsync(CreateSeriesDto input);
}
