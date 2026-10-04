using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Emailing;
using Volo.Abp.Modularity;
using Volo.Abp.Settings;
using Xunit;

namespace Dixels.Emailing;

public abstract class EmailSetupTests<TStartupModule> : DixelsDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    [Fact]
    public void Tests_send_through_the_fake_sender()
    {
        GetRequiredService<IEmailSender>().ShouldBeOfType<FakeEmailSender>();
    }

    [Fact]
    public async Task Smtp_password_is_read_as_plain_text()
    {
        // It comes from an env var; if ABP tried to decrypt it, logging in to the mail server would fail.
        var password = await GetRequiredService<ISettingDefinitionManager>().GetAsync(EmailSettingNames.Smtp.Password);

        password.IsEncrypted.ShouldBeFalse();
    }
}
