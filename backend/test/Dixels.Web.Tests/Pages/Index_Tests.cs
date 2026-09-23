using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Dixels.Pages;

public class Index_Tests : DixelsWebTestBase
{
    [Fact]
    public async Task Welcome_Page()
    {
        var response = await GetResponseAsStringAsync("/");
        response.ShouldNotBeNull();
    }
}
