using System.Threading.Tasks;
using Dixels.Users;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Controllers.Users;

[RemoteService(Name = DixelsRemoteServiceConsts.RemoteServiceName)]
[Area(DixelsRemoteServiceConsts.ModuleName)]
[Route("api/app/colleagues")]
public class ColleaguesController : DixelsController, IColleaguesAppService
{
    private readonly IColleaguesAppService _colleaguesAppService;

    public ColleaguesController(IColleaguesAppService colleaguesAppService)
    {
        _colleaguesAppService = colleaguesAppService;
    }

    [HttpGet]
    public virtual Task<ListResultDto<ColleagueDto>> GetListAsync([FromQuery] GetColleaguesInput input) =>
        _colleaguesAppService.GetListAsync(input);
}
