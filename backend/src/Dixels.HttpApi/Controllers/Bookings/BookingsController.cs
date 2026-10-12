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
    public virtual async Task<BookingPreviewDto> PreviewAsync([FromBody] BookingRequestDto input) =>
        await _bookingsAppService.PreviewAsync(input);

    [HttpPost]
    public virtual async Task<BookingDto> CreateAsync([FromBody] CreateBookingDto input) => await _bookingsAppService.CreateAsync(input);

    [HttpGet("mine")]
    public virtual async Task<ListResultDto<BookingSummaryDto>> GetMineAsync([FromQuery] GetMyBookingsInput input) =>
        await _bookingsAppService.GetMineAsync(input);

    [HttpGet("{id}")]
    public virtual async Task<BookingDto> GetAsync(Guid id) => await _bookingsAppService.GetAsync(id);

    [HttpPost("{id}/cancel")]
    public virtual async Task<ListResultDto<BookingDto>> CancelAsync(Guid id, [FromBody] CancelBookingDto input) =>
        await _bookingsAppService.CancelAsync(id, input);

    [HttpPost("{id}/response")]
    public virtual async Task<BookingDto> RespondAsync(Guid id, [FromBody] RespondToInviteDto input) =>
        await _bookingsAppService.RespondAsync(id, input);

    [HttpPost("series/{seriesId}/response")]
    public virtual async Task RespondToSeriesAsync(Guid seriesId, [FromBody] RespondToInviteDto input) =>
        await _bookingsAppService.RespondToSeriesAsync(seriesId, input);

    [HttpPost("series/preview")]
    public virtual async Task<SeriesPreviewDto> PreviewSeriesAsync([FromBody] SeriesRequestDto input) =>
        await _bookingsAppService.PreviewSeriesAsync(input);

    [HttpPut("{id}/invitees")]
    public virtual async Task<BookingDto> UpdateInviteesAsync(Guid id, [FromBody] UpdateInviteesDto input) =>
        await _bookingsAppService.UpdateInviteesAsync(id, input);

    // POST, not GET: the ids go in the body (up to 50 would make a long URL). Reads only.
    [HttpPost("{id}/busy-guests")]
    public virtual async Task<BusyGuestsResultDto> GetBusyGuestsAsync(Guid id, [FromBody] BusyGuestsInput input) =>
        await _bookingsAppService.GetBusyGuestsAsync(id, input);

    [HttpPost("series/{seriesId}/busy-guests")]
    public virtual async Task<BusyGuestsResultDto> GetSeriesBusyGuestsAsync(Guid seriesId, [FromBody] BusyGuestsInput input) =>
        await _bookingsAppService.GetSeriesBusyGuestsAsync(seriesId, input);

    [HttpPut("series/{seriesId}/invitees")]
    public virtual async Task<SeriesCreatedDto> UpdateSeriesInviteesAsync(Guid seriesId, [FromBody] UpdateInviteesDto input) =>
        await _bookingsAppService.UpdateSeriesInviteesAsync(seriesId, input);

    [HttpPost("series")]
    public virtual async Task<SeriesCreatedDto> CreateSeriesAsync([FromBody] CreateSeriesDto input) =>
        await _bookingsAppService.CreateSeriesAsync(input);
}
