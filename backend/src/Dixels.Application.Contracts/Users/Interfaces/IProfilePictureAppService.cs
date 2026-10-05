using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Dixels.Users;

/// <summary>
/// The signed-in person's own profile picture (<c>/api/app/profile-picture</c>): My profile
/// sets and removes it, the sidebar and My profile show it.
/// </summary>
public interface IProfilePictureAppService : IApplicationService
{
    /// <summary>The picture, or nothing (204) when there isn't one.</summary>
    Task<IRemoteStreamContent?> GetAsync();

    /// <summary>Replaces the picture with this JPEG, PNG or WebP file (form field <c>file</c>).</summary>
    Task UpdateAsync(IRemoteStreamContent file);

    Task DeleteAsync();
}
