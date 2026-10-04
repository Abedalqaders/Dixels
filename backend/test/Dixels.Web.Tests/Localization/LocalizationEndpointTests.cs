using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Dixels.Localization;

/// <summary>
/// The web app has no texts of its own: before anyone signs in (the sign-in page is already
/// translated) it reads the language list and every text from ABP's endpoints. Both must
/// answer an anonymous caller.
/// </summary>
public class LocalizationEndpointTests : DixelsWebTestBase
{
    [Fact]
    public async Task The_language_list_is_public_and_lists_the_app_languages()
    {
        var json = await GetResponseAsStringAsync("/api/abp/application-configuration?includeLocalizationResources=false");

        using var doc = JsonDocument.Parse(json);
        var languages = doc.RootElement.GetProperty("localization").GetProperty("languages")
            .EnumerateArray()
            .Select(l => l.GetProperty("cultureName").GetString())
            .ToList();

        languages.ShouldBe(new[] { "en", "ar" }, ignoreOrder: true);
    }

    [Fact]
    public async Task The_texts_of_a_language_are_public()
    {
        var json = await GetResponseAsStringAsync("/api/abp/application-localization?cultureName=ar&onlyDynamics=false");

        using var doc = JsonDocument.Parse(json);
        var texts = doc.RootElement.GetProperty("resources").GetProperty("Dixels").GetProperty("texts");

        texts.GetProperty("Nav:Hierarchy").GetString().ShouldBe("الهيكل");
        texts.GetProperty("Dixels:SpaceManagement:SpaceTypeInUse").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
