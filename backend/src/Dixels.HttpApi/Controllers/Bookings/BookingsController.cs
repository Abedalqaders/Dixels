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

    [HttpPost("{id}/response")]
    public virtual Task<BookingDto> RespondAsync(Guid id, [FromBody] RespondToInviteDto input) =>
        _bookingsAppService.RespondAsync(id, input);

    [HttpPost("series/{seriesId}/response")]
    public virtual Task RespondToSeriesAsync(Guid seriesId, [FromBody] RespondToInviteDto input) =>
        _bookingsAppService.RespondToSeriesAsync(seriesId, input);

    [HttpPost("series/preview")]
    public virtual Task<SeriesPreviewDto> PreviewSeriesAsync([FromBody] SeriesRequestDto input) =>
        _bookingsAppService.PreviewSeriesAsync(input);

    [HttpPut("{id}/invitees")]
    public virtual Task<BookingDto> UpdateInviteesAsync(Guid id, [FromBody] UpdateInviteesDto input) =>
        _bookingsAppService.UpdateInviteesAsync(id, input);

    [HttpPut("series/{seriesId}/invitees")]
    public virtual Task<SeriesCreatedDto> UpdateSeriesInviteesAsync(Guid seriesId, [FromBody] UpdateInviteesDto input) =>
        _bookingsAppService.UpdateSeriesInviteesAsync(seriesId, input);

    [HttpPost("series")]
    public virtual Task<SeriesCreatedDto> CreateSeriesAsync([FromBody] CreateSeriesDto input) =>
        _bookingsAppService.CreateSeriesAsync(input);
}
