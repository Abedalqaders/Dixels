using System.Threading.Tasks;
using Dixels.Users;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Controllers.Users;

[RemoteService(Name = DixelsRemoteServiceConsts.RemoteServiceName)]
[Area(DixelsRemoteServiceConsts.ModuleName)]
[Route("api/app/my-preferences")]
public class MyPreferencesController : DixelsController, IMyPreferencesAppService
{
    private readonly IMyPreferencesAppService _myPreferencesAppService;

    public MyPreferencesController(IMyPreferencesAppService myPreferencesAppService)
    {
        _myPreferencesAppService = myPreferencesAppService;
    }

    [HttpPut("language")]
    public virtual async Task UpdateLanguageAsync([FromBody] UpdateMyLanguageDto input) =>
        await _myPreferencesAppService.UpdateLanguageAsync(input);
}
