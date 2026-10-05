using System.Threading.Tasks;
using Dixels.Users;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Content;

namespace Dixels.Controllers.Users;

// The signed-in person's own picture. The upload is a form with the file in the field "file"
// (frontend profileApi.ts setMyPicture).
[RemoteService(Name = DixelsRemoteServiceConsts.RemoteServiceName)]
[Area(DixelsRemoteServiceConsts.ModuleName)]
[Route("api/app/profile-picture")]
public class ProfilePictureController : DixelsController, IProfilePictureAppService
{
    private readonly IProfilePictureAppService _profilePictureAppService;

    public ProfilePictureController(IProfilePictureAppService profilePictureAppService)
    {
        _profilePictureAppService = profilePictureAppService;
    }

    /// <summary>The picture, or 204 No Content when there isn't one.</summary>
    [HttpGet]
    public virtual Task<IRemoteStreamContent?> GetAsync() => _profilePictureAppService.GetAsync();

    [HttpPut]
    public virtual Task UpdateAsync(IRemoteStreamContent file) => _profilePictureAppService.UpdateAsync(file);

    [HttpDelete]
    public virtual Task DeleteAsync() => _profilePictureAppService.DeleteAsync();
}
