using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Users;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.EntityFrameworkCore.Users;

/// <summary>
/// A person's own profile picture: kept, replaced, removed — and only when it really is a
/// JPEG, PNG or WebP that fits the size limit.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class ProfilePictureTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 5, 6, 7];
    private static readonly byte[] WebP = [.. "RIFF"u8.ToArray(), 0, 0, 0, 0, .. "WEBP"u8.ToArray(), 9];

    private readonly IProfilePictureAppService _pictures;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public ProfilePictureTests()
    {
        _pictures = GetRequiredService<IProfilePictureAppService>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable ActAs(Guid userId)
    {
        return _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, userId.ToString()),
        })));
    }

    // What the browser sends: a file whose own content type may say anything.
    private static RemoteStreamContent File(byte[] bytes, string claimedType = "image/png") =>
        new(new MemoryStream(bytes), "picture", claimedType);

    private async Task<(byte[] Bytes, string? ContentType)?> ReadAsync()
    {
        var content = await _pictures.GetAsync();
        if (content is null)
        {
            return null;
        }

        using var copy = new MemoryStream();
        await content.GetStream().CopyToAsync(copy);
        return (copy.ToArray(), content.ContentType);
    }

    [Fact]
    public async Task Has_none_until_one_is_set()
    {
        using (ActAs(Guid.NewGuid()))
        {
            (await ReadAsync()).ShouldBeNull();
        }
    }

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("webp", "image/webp")]
    public async Task Keeps_a_picture_and_says_what_type_it_is(string kind, string expectedType)
    {
        var bytes = kind switch { "png" => Png, "jpeg" => Jpeg, _ => WebP };
        using (ActAs(Guid.NewGuid()))
        {
            // The type the file claims is ignored: the bytes decide.
            await _pictures.UpdateAsync(File(bytes, claimedType: "application/octet-stream"));

            var stored = (await ReadAsync()).ShouldNotBeNull();
            stored.Bytes.ShouldBe(bytes);
            stored.ContentType.ShouldBe(expectedType);
        }
    }

    [Fact]
    public async Task Replaces_and_removes_it()
    {
        using (ActAs(Guid.NewGuid()))
        {
            await _pictures.UpdateAsync(File(Png));
            await _pictures.UpdateAsync(File(Jpeg));
            (await ReadAsync()).ShouldNotBeNull().Bytes.ShouldBe(Jpeg);

            await _pictures.DeleteAsync();
            (await ReadAsync()).ShouldBeNull();

            // Removing again is not an error.
            await _pictures.DeleteAsync();
        }
    }

    [Fact]
    public async Task Each_person_sees_only_their_own()
    {
        using (ActAs(Guid.NewGuid()))
        {
            await _pictures.UpdateAsync(File(Png));
        }

        using (ActAs(Guid.NewGuid()))
        {
            (await ReadAsync()).ShouldBeNull();
        }
    }

    [Fact]
    public async Task Refuses_a_file_that_is_not_a_picture_whatever_it_claims()
    {
        using (ActAs(Guid.NewGuid()))
        {
            var error = await Should.ThrowAsync<BusinessException>(() =>
                _pictures.UpdateAsync(File("<svg onload=alert(1)>"u8.ToArray(), claimedType: "image/png")));

            error.Code.ShouldBe(DixelsDomainErrorCodes.ProfilePictureNotAnImage);
            (await ReadAsync()).ShouldBeNull();
        }
    }

    [Fact]
    public async Task Refuses_one_over_the_size_limit_and_keeps_the_old_one()
    {
        var tooBig = Png.Concat(new byte[ProfilePictureConsts.MaxBytes]).ToArray();
        using (ActAs(Guid.NewGuid()))
        {
            await _pictures.UpdateAsync(File(Jpeg));

            var error = await Should.ThrowAsync<BusinessException>(() => _pictures.UpdateAsync(File(tooBig)));

            error.Code.ShouldBe(DixelsDomainErrorCodes.ProfilePictureTooLarge);
            (await ReadAsync()).ShouldNotBeNull().Bytes.ShouldBe(Jpeg);
        }
    }

    [Fact]
    public void Recognises_pictures_by_their_first_bytes_only()
    {
        ProfilePictureManager.ContentTypeOf(Png).ShouldBe("image/png");
        ProfilePictureManager.ContentTypeOf(Jpeg).ShouldBe("image/jpeg");
        ProfilePictureManager.ContentTypeOf(WebP).ShouldBe("image/webp");
        ProfilePictureManager.ContentTypeOf("GIF89a"u8).ShouldBeNull();
        ProfilePictureManager.ContentTypeOf([0xFF, 0xD8]).ShouldBeNull();
        ProfilePictureManager.ContentTypeOf([]).ShouldBeNull();
    }
}
