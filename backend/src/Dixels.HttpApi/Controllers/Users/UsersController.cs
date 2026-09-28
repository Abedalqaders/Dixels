using System;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.Users;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Controllers.Users;

// Only my-building lives here — listing users and assigning their building use ABP's own
// /api/identity/users (DixelsIdentityUserAppService).
[RemoteService(Name = DixelsRemoteServiceConsts.RemoteServiceName)]
[Area(DixelsRemoteServiceConsts.ModuleName)]
[Route("api/app/users")]
public class UsersController : DixelsController, IUsersAppService
{
    private readonly IUsersAppService _usersAppService;

    public UsersController(IUsersAppService usersAppService)
    {
        _usersAppService = usersAppService;
    }

    [HttpGet("my-building")]
    public virtual Task<BuildingDto?> GetMyBuildingAsync() => _usersAppService.GetMyBuildingAsync();

    [HttpGet("{userId}/reassign-impact")]
    public virtual Task<BookingImpactDto> GetReassignImpactAsync(Guid userId) => _usersAppService.GetReassignImpactAsync(userId);

    [HttpPut("{userId}/building")]
    public virtual Task AssignBuildingAsync(Guid userId, [FromBody] AssignUserBuildingDto input) =>
        _usersAppService.AssignBuildingAsync(userId, input);
}
