using Microsoft.AspNetCore.Builder;
using Dixels;
using Volo.Abp.AspNetCore.TestBase;

var builder = WebApplication.CreateBuilder();

builder.Environment.ContentRootPath = GetWebProjectContentRootPathHelper.Get("Dixels.Web.csproj");
// The host runs as Production here, which requires the key that signs guests' answer links.
builder.Configuration["GuestLinks:Key"] = "web-tests-guest-links-key";
await builder.RunAbpModuleAsync<DixelsWebTestModule>(applicationName: "Dixels.Web" );

public partial class Program
{
}
