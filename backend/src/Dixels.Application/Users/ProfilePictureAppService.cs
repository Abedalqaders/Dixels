using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Users;

namespace Dixels.Users;

/// <summary>
/// The signed-in person's own picture — anyone signed in, for themselves only. What a picture
/// may be is <see cref="ProfilePictureManager"/>'s call.
/// </summary>
[Authorize]
public class ProfilePictureAppService : DixelsAppService, IProfilePictureAppService
{
    private readonly ProfilePictureManager _pictures;

    public ProfilePictureAppService(ProfilePictureManager pictures)
    {
        _pictures = pictures;
    }

    public async Task<IRemoteStreamContent?> GetAsync()
    {
        var picture = await _pictures.GetOrNullAsync(CurrentUser.GetId());
        return picture is null
            ? null
            : new RemoteStreamContent(new MemoryStream(picture.Content), "profile-picture", picture.ContentType);
    }

    public async Task UpdateAsync(IRemoteStreamContent file)
    {
        Check.NotNull(file, nameof(file));
        await _pictures.SetAsync(CurrentUser.GetId(), await ReadAtMostAsync(file, ProfilePictureConsts.MaxBytes + 1));
    }

    public async Task DeleteAsync()
    {
        await _pictures.DeleteAsync(CurrentUser.GetId());
    }

    // Reads no more than `limit` bytes, whatever the file says its length is: one byte past
    // the maximum is enough for the manager to refuse it as too large.
    private static async Task<byte[]> ReadAtMostAsync(IRemoteStreamContent file, int limit)
    {
        await using var source = file.GetStream();
        using var copy = new MemoryStream();
        var buffer = new byte[81920];
        while (copy.Length < limit)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, limit - copy.Length)));
            if (read == 0)
            {
                break;
            }

            copy.Write(buffer, 0, read);
        }

        return copy.ToArray();
    }
}
