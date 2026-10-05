using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Services;

namespace Dixels.Users;

/// <summary>A stored picture and the type it was recognised as ("image/png").</summary>
public record ProfilePicture(byte[] Content, string ContentType);

/// <summary>
/// People's profile pictures: one per user, kept in <see cref="ProfilePictureContainer"/>. A
/// picture is only kept if its bytes really are a JPEG, PNG or WebP — what a file claims to be
/// (its name, its content type) is never trusted — and if it fits
/// <see cref="ProfilePictureConsts.MaxBytes"/>.
/// </summary>
public class ProfilePictureManager : DomainService
{
    private readonly IBlobContainer<ProfilePictureContainer> _container;

    public ProfilePictureManager(IBlobContainer<ProfilePictureContainer> container)
    {
        _container = container;
    }

    public async Task SetAsync(Guid userId, byte[] content)
    {
        if (content.Length > ProfilePictureConsts.MaxBytes)
        {
            throw new BusinessException(DixelsDomainErrorCodes.ProfilePictureTooLarge)
                .WithData("maxKb", ProfilePictureConsts.MaxBytes / 1024);
        }

        if (ContentTypeOf(content) is null)
        {
            throw new BusinessException(DixelsDomainErrorCodes.ProfilePictureNotAnImage);
        }

        await _container.SaveAsync(BlobName(userId), content, overrideExisting: true);
    }

    /// <summary>The user's picture, or null when they have none.</summary>
    public async Task<ProfilePicture?> GetOrNullAsync(Guid userId)
    {
        var content = await _container.GetAllBytesOrNullAsync(BlobName(userId));
        if (content is null)
        {
            return null;
        }

        return new ProfilePicture(content, ContentTypeOf(content) ?? "application/octet-stream");
    }

    /// <summary>Removes the user's picture; nothing happens when they have none.</summary>
    public Task DeleteAsync(Guid userId) => _container.DeleteAsync(BlobName(userId));

    /// <summary>The image type the first bytes say this is, or null when they're not one of
    /// the three kept: JPEG (FF D8 FF), PNG (89 "PNG" 0D 0A 1A 0A), WebP ("RIFF", size, "WEBP").</summary>
    public static string? ContentTypeOf(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (content.Length >= 8 && content[..8].SequenceEqual(PngSignature))
        {
            return "image/png";
        }

        if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static string BlobName(Guid userId) => userId.ToString("N");
}
