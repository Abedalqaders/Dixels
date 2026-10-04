using System;
using System.Threading.Tasks;
using Dixels.Bookings;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Controllers.Bookings;

/// <summary>
/// What an employee can book: their building with its rooms (<c>my-building</c>), which
/// rooms are free for a time window (<c>search</c>), and one room's days (<c>{spaceId}/days</c>).
/// </summary>
[RemoteService(Name = DixelsRemoteServiceConsts.RemoteServiceName)]
[Area(DixelsRemoteServiceConsts.ModuleName)]
[Route("api/app/bookable-spaces")]
public class BookableSpacesController : DixelsController, IAvailabilityAppService
{
    private readonly IAvailabilityAppService _availabilityAppService;

    public BookableSpacesController(IAvailabilityAppService availabilityAppService)
    {
        _availabilityAppService = availabilityAppService;
    }

    [HttpGet("my-building")]
    public virtual Task<BookableBuildingDto?> GetMyBuildingAsync() => _availabilityAppService.GetMyBuildingAsync();

    [HttpGet("search")]
    public virtual Task<AvailabilitySearchResultDto> SearchAsync([FromQuery] SearchAvailabilityInput input) =>
        _availabilityAppService.SearchAsync(input);

    [HttpGet("{spaceId}/days")]
    public virtual Task<SpaceDaysDto> GetSpaceDaysAsync(Guid spaceId, [FromQuery] GetSpaceDaysInput input) =>
        _availabilityAppService.GetSpaceDaysAsync(spaceId, input);
}
