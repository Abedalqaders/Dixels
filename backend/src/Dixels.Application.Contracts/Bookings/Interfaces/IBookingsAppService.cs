using System.Threading.Tasks;
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
}
