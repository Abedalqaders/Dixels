using Dixels.Emailing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Emailing;
using Volo.Abp.Modularity;

namespace Dixels;

[DependsOn(
    typeof(DixelsDomainModule),
    typeof(DixelsTestBaseModule)
)]
public class DixelsDomainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // No test talks to a mail server; FakeEmailSender records what would have been sent.
        context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, FakeEmailSender>());
        context.Services.AddSingleton(sp => (FakeEmailSender)sp.GetRequiredService<IEmailSender>());
    }
}
