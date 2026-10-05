using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Users;

namespace Dixels.Users;

[Authorize]
public class MyPreferencesAppService : DixelsAppService, IMyPreferencesAppService
{
    private readonly UserLanguageManager _userLanguage;

    public MyPreferencesAppService(UserLanguageManager userLanguage)
    {
        _userLanguage = userLanguage;
    }

    public async Task UpdateLanguageAsync(UpdateMyLanguageDto input)
    {
        await _userLanguage.SetAsync(CurrentUser.GetId(), input.Language);
    }
}
