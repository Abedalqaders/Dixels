using Microsoft.AspNetCore.Builder;
using Dixels;
using Volo.Abp.AspNetCore.TestBase;

var builder = WebApplication.CreateBuilder();
builder.Environment.ContentRootPath = GetWebProjectContentRootPathHelper.Get("Dixels.Web.csproj"); 
await builder.RunAbpModuleAsync<DixelsWebTestModule>(applicationName: "Dixels.Web");

public partial class Program
{
}
