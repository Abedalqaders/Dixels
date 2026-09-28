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
    public virtual Task<ListResultDto<BookingDto>> GetMineAsync([FromQuery] GetMyBookingsInput input) =>
        _bookingsAppService.GetMineAsync(input);

    [HttpPost("{id}/cancel")]
    public virtual Task<ListResultDto<BookingDto>> CancelAsync(Guid id, [FromBody] CancelBookingDto input) =>
        _bookingsAppService.CancelAsync(id, input);

    [HttpPost("series/preview")]
    public virtual Task<SeriesPreviewDto> PreviewSeriesAsync([FromBody] SeriesRequestDto input) =>
        _bookingsAppService.PreviewSeriesAsync(input);

    [HttpPost("series")]
    public virtual Task<SeriesCreatedDto> CreateSeriesAsync([FromBody] CreateSeriesDto input) =>
        _bookingsAppService.CreateSeriesAsync(input);
}
