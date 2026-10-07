using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Users;

public interface IColleaguesAppService : IApplicationService
{
    /// <summary>
    /// Active people in the caller's building matching the filter (never the caller), for
    /// the booking form's guest picker. Empty for someone not assigned to a building.
    /// </summary>
    Task<ListResultDto<ColleagueDto>> GetListAsync(GetColleaguesInput input);
}
