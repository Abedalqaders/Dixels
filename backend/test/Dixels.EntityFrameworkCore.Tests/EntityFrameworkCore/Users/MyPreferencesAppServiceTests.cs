using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Users;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.EntityFrameworkCore.Users;

/// <summary>The language the web app saves for the signed-in user, and what emails read back.</summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class MyPreferencesAppServiceTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IMyPreferencesAppService _myPreferences;
    private readonly UserLanguageManager _userLanguage;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public MyPreferencesAppServiceTests()
    {
        _myPreferences = GetRequiredService<IMyPreferencesAppService>();
        _userLanguage = GetRequiredService<UserLanguageManager>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable ActAs(Guid userId)
    {
        return _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, userId.ToString()),
        })));
    }

    private Task<string> LanguageOfAsync(Guid userId) => WithUnitOfWorkAsync(() => _userLanguage.GetAsync(userId));

    [Fact]
    public async Task A_user_who_never_saved_one_gets_the_default_language()
    {
        (await LanguageOfAsync(Guid.NewGuid())).ShouldBe("en");
    }

    [Fact]
    public async Task Saves_the_language_for_the_signed_in_user_only()
    {
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();

        using (ActAs(user))
        {
            await _myPreferences.UpdateLanguageAsync(new UpdateMyLanguageDto { Language = "ar" });
        }

        (await LanguageOfAsync(user)).ShouldBe("ar");
        (await LanguageOfAsync(other)).ShouldBe("en");
    }

    [Fact]
    public async Task The_last_saved_language_wins()
    {
        var user = Guid.NewGuid();

        using (ActAs(user))
        {
            await _myPreferences.UpdateLanguageAsync(new UpdateMyLanguageDto { Language = "ar" });
            await _myPreferences.UpdateLanguageAsync(new UpdateMyLanguageDto { Language = "en" });
        }

        (await LanguageOfAsync(user)).ShouldBe("en");
    }

    [Fact]
    public async Task Rejects_a_language_the_app_does_not_have()
    {
        var user = Guid.NewGuid();

        using (ActAs(user))
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _myPreferences.UpdateLanguageAsync(new UpdateMyLanguageDto { Language = "xx" }));
            ex.Code.ShouldBe(DixelsDomainErrorCodes.UnsupportedLanguage);
        }

        (await LanguageOfAsync(user)).ShouldBe("en");
    }
}
