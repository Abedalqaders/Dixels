using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dixels.Users;

/// <summary>The signed-in user's own preferences — each user changes only theirs.</summary>
public interface IMyPreferencesAppService : IApplicationService
{
    /// <summary>
    /// The language they're using the app in. The web app sends it on sign-in and whenever
    /// they switch; emails to them are written in the last one.
    /// </summary>
    Task UpdateLanguageAsync(UpdateMyLanguageDto input);
}
