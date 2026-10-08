using System;
using System.Threading.Tasks;
using Dixels.Bookings;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Controllers.Bookings;

[RemoteService(Name = DixelsRemoteServiceConsts.RemoteServiceName)]
[Area(DixelsRemoteServiceConsts.ModuleName)]
[Route("api/app/bookings")]
public class BookingsController : DixelsController, IBookingsAppService
{
    private readonly IBookingsAppService _bookingsAppService;

    public BookingsController(IBookingsAppService bookingsAppService)
    {
        _bookingsAppService = bookingsAppService;
    }

    [HttpPost("preview")]
    public virtual Task<BookingPreviewDto> PreviewAsync([FromBody] BookingRequestDto input) =>
        _bookingsAppService.PreviewAsync(input);

    [HttpPost]
    public virtual Task<BookingDto> CreateAsync([FromBody] CreateBookingDto input) => _bookingsAppService.CreateAsync(input);

    [HttpGet("mine")]
    public virtual Task<ListResultDto<BookingSummaryDto>> GetMineAsync([FromQuery] GetMyBookingsInput input) =>
        _bookingsAppService.GetMineAsync(input);

    [HttpGet("{id}")]
    public virtual Task<BookingDto> GetAsync(Guid id) => _bookingsAppService.GetAsync(id);

    [HttpPost("{id}/cancel")]
    public virtual Task<ListResultDto<BookingDto>> CancelAsync(Guid id, [FromBody] CancelBookingDto input) =>
        _bookingsAppService.CancelAsync(id, input);

    [HttpPost("series/preview")]
    public virtual Task<SeriesPreviewDto> PreviewSeriesAsync([FromBody] SeriesRequestDto input) =>
        _bookingsAppService.PreviewSeriesAsync(input);

    [HttpPut("{id}/invitees")]
    public virtual Task<BookingDto> UpdateInviteesAsync(Guid id, [FromBody] UpdateInviteesDto input) =>
        _bookingsAppService.UpdateInviteesAsync(id, input);

    // POST, not GET: the ids go in the body (up to 50 would make a long URL). Reads only.
    [HttpPost("{id}/busy-guests")]
    public virtual Task<BusyGuestsResultDto> GetBusyGuestsAsync(Guid id, [FromBody] BusyGuestsInput input) =>
        _bookingsAppService.GetBusyGuestsAsync(id, input);

    [HttpPost("series/{seriesId}/busy-guests")]
    public virtual Task<BusyGuestsResultDto> GetSeriesBusyGuestsAsync(Guid seriesId, [FromBody] BusyGuestsInput input) =>
        _bookingsAppService.GetSeriesBusyGuestsAsync(seriesId, input);

    [HttpPut("series/{seriesId}/invitees")]
    public virtual Task<SeriesCreatedDto> UpdateSeriesInviteesAsync(Guid seriesId, [FromBody] UpdateInviteesDto input) =>
        _bookingsAppService.UpdateSeriesInviteesAsync(seriesId, input);

    [HttpPost("series")]
    public virtual Task<SeriesCreatedDto> CreateSeriesAsync([FromBody] CreateSeriesDto input) =>
        _bookingsAppService.CreateSeriesAsync(input);
}
