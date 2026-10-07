using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Users;

namespace Dixels.Users;

/// <summary>
/// The guest picker's search. Any signed-in employee may use it, so it shows only what
/// picking needs (name and email) and only their own building's people: the same ones a
/// booking there accepts as colleagues (see BookingInviteeResolver).
/// </summary>
[Authorize]
public class ColleaguesAppService : DixelsAppService, IColleaguesAppService
{
    private readonly IUserDirectoryRepository _userDirectory;
    private readonly BookingAccessChecker _accessChecker;

    public ColleaguesAppService(IUserDirectoryRepository userDirectory, BookingAccessChecker accessChecker)
    {
        _userDirectory = userDirectory;
        _accessChecker = accessChecker;
    }

    public async Task<ListResultDto<ColleagueDto>> GetListAsync(GetColleaguesInput input)
    {
        var filter = input.Filter?.Trim();
        if (filter is null || filter.Length < GetColleaguesInput.MinFilterLength)
        {
            return new ListResultDto<ColleagueDto>();
        }

        var me = CurrentUser.GetId();
        var buildingId = await _accessChecker.FindBookableBuildingIdAsync(me);
        if (buildingId is null)
        {
            return new ListResultDto<ColleagueDto>();
        }

        var users = await _userDirectory.SearchColleaguesAsync(
            buildingId.Value, filter, me, Math.Min(input.MaxResultCount, GetColleaguesInput.MaxMaxResultCount));

        return new ListResultDto<ColleagueDto>(users
            .Select(u => new ColleagueDto { Id = u.Id, Name = u.DisplayName(), Email = u.Email ?? string.Empty })
            .ToList());
    }
}
