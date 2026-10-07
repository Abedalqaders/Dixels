using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.Reservations;
using Dixels.Users;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Controllers.Users;

// User endpoints ABP's Identity module doesn't have. Listing, creating and editing users is
// ABP's own /api/identity/users (DixelsIdentityUserAppService).
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

    [HttpGet("{userId}/reassign-impact")]
    public virtual Task<ReservationImpactDto> GetReassignImpactAsync(Guid userId, [FromQuery] int skip = 0) => _usersAppService.GetReassignImpactAsync(userId, skip);

    [HttpPut("{userId}/building")]
    public virtual Task AssignBuildingAsync(Guid userId, [FromBody] AssignUserBuildingDto input) =>
        _usersAppService.AssignBuildingAsync(userId, input);

    [HttpGet("page-details")]
    public virtual Task<List<UserPageDetailsDto>> GetPageDetailsAsync([FromQuery] GetUserPageDetailsInput input) =>
        _usersAppService.GetPageDetailsAsync(input);

    [HttpGet("role-names")]
    public virtual Task<List<string>> GetRoleNamesAsync() => _usersAppService.GetRoleNamesAsync();
}
