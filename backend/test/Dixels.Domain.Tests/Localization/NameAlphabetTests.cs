using Shouldly;
using Xunit;

namespace Dixels.Localization.Tests;

public class NameAlphabetTests
{
    [Theory]
    [InlineData("en", "Meeting room 101")]
    [InlineData("en", "Café (north)")]
    [InlineData("en", "101")]
    [InlineData("ar", "غرفة اجتماعات")]
    [InlineData("ar", "غرفة ١٠١")]
    [InlineData("ar", "مكتـــب")] // tatweel, the stretching line
    [InlineData("ar", "غرفة IT")]
    [InlineData("ar", "غرفة B12")]
    [InlineData("ar", "كابينة 3-01")]
    [InlineData("ar", "101")]
    [InlineData("ar-JO", "قاعة VIP")]
    [InlineData("xx", "Anything at all")] // a language the table doesn't know isn't checked
    public void Fits(string language, string name)
    {
        NameAlphabet.Fits(language, name).ShouldBeTrue();
    }

    [Theory]
    [InlineData("en", "غرفة")]
    [InlineData("en", "Room غرفة")]
    [InlineData("en", "Room IT غرفة")]
    [InlineData("ar", "Meeting room")]
    [InlineData("ar", "غرفة meeting")]
    [InlineData("ar", "غرفة MEETING")] // capitals, but too long for a code
    [InlineData("ar", "غرفةIT")] // a code has to stand on its own
    [InlineData("ar", "IT")] // codes only: no Arabic letter at all
    [InlineData("ar", "B12")]
    public void Does_Not_Fit(string language, string name)
    {
        NameAlphabet.Fits(language, name).ShouldBeFalse();
    }

    [Fact]
    public void Only_Languages_Not_Written_In_Latin_Letters_Allow_Codes()
    {
        NameAlphabet.AllowsLatinCodes("ar").ShouldBeTrue();
        NameAlphabet.AllowsLatinCodes("en").ShouldBeFalse();
        NameAlphabet.AllowsLatinCodes("xx").ShouldBeFalse();
    }
}
