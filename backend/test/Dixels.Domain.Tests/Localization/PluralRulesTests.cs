using System.Globalization;
using System.Linq;
using Shouldly;
using Xunit;

namespace Dixels.Localization.Tests;

public class PluralRulesTests
{
    // The expected forms are what Intl.PluralRules (CLDR, as the web app uses it) answers:
    //   new Intl.PluralRules("ru").select(22) === "few"
    // so the server and the browser pick the same translation for a count.
    [Theory]
    [InlineData("en", "0:other 1:one 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:other")]
    [InlineData("fr", "0:one 1:one 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:many")]
    [InlineData("pt", "0:one 1:one 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:many")]
    [InlineData("es", "0:other 1:one 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:many")]
    [InlineData("it", "0:other 1:one 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:many")]
    [InlineData("fa", "0:one 1:one 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:other")]
    [InlineData("hi", "0:one 1:one 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:other")]
    [InlineData("ru", "0:many 1:one 2:few 3:few 4:few 5:many 10:many 11:many 12:many 14:many 19:many 20:many 21:one 22:few 25:many 101:one 102:few 103:few 111:many 112:many 1000000:many")]
    [InlineData("uk", "0:many 1:one 2:few 3:few 4:few 5:many 10:many 11:many 12:many 14:many 19:many 20:many 21:one 22:few 25:many 101:one 102:few 103:few 111:many 112:many 1000000:many")]
    [InlineData("pl", "0:many 1:one 2:few 3:few 4:few 5:many 10:many 11:many 12:many 14:many 19:many 20:many 21:many 22:few 25:many 101:many 102:few 103:few 111:many 112:many 1000000:many")]
    [InlineData("cs", "0:other 1:one 2:few 3:few 4:few 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:other")]
    [InlineData("ro", "0:few 1:one 2:few 3:few 4:few 5:few 10:few 11:few 12:few 14:few 19:few 20:other 21:other 22:other 25:other 101:few 102:few 103:few 111:few 112:few 1000000:other")]
    [InlineData("he", "0:other 1:one 2:two 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:other")]
    [InlineData("ar", "0:zero 1:one 2:two 3:few 4:few 5:few 10:few 11:many 12:many 14:many 19:many 20:many 21:many 22:many 25:many 101:other 102:other 103:few 111:many 112:many 1000000:other")]
    [InlineData("id", "0:other 1:other 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:other")]
    [InlineData("zh", "0:other 1:other 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:other")]
    [InlineData("ja", "0:other 1:other 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:other")]
    [InlineData("de", "0:other 1:one 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:other")]
    [InlineData("tr", "0:other 1:one 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:other")]
    [InlineData("bg", "0:other 1:one 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:other")]
    [InlineData("el", "0:other 1:one 2:other 3:other 4:other 5:other 10:other 11:other 12:other 14:other 19:other 20:other 21:other 22:other 25:other 101:other 102:other 103:other 111:other 112:other 1000000:other")]
    public void Matches_the_browsers_plural_rules(string language, string expected)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        foreach (var pair in expected.Split(' ').Select(p => p.Split(':')))
        {
            PluralRules.FormOf(culture, long.Parse(pair[0])).ShouldBe(pair[1], $"{language} {pair[0]}");
        }
    }

    [Fact]
    public void A_language_not_in_the_table_counts_like_english()
    {
        var culture = CultureInfo.GetCultureInfo("sw");

        PluralRules.FormOf(culture, 1).ShouldBe("one");
        PluralRules.FormOf(culture, 0).ShouldBe("other");
        PluralRules.FormOf(culture, 5).ShouldBe("other");
    }

    [Fact]
    public void A_regional_culture_follows_its_language()
    {
        PluralRules.FormOf(CultureInfo.GetCultureInfo("ar-JO"), 2).ShouldBe("two");
    }
}
