using System.Threading.Tasks;
using Dixels.Bookings;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Controllers.Bookings;

[RemoteService(Name = DixelsRemoteServiceConsts.RemoteServiceName)]
[Area(DixelsRemoteServiceConsts.ModuleName)]
[Route("api/app/availability")]
public class AvailabilityController : DixelsController, IAvailabilityAppService
{
    private readonly IAvailabilityAppService _availabilityAppService;

    public AvailabilityController(IAvailabilityAppService availabilityAppService)
    {
        _availabilityAppService = availabilityAppService;
    }

    [HttpGet("my-building")]
    public virtual Task<BookableBuildingDto?> GetMyBuildingAsync() => _availabilityAppService.GetMyBuildingAsync();

    [HttpGet("search")]
    public virtual Task<AvailabilitySearchResultDto> SearchAsync([FromQuery] SearchAvailabilityInput input) =>
        _availabilityAppService.SearchAsync(input);
}
