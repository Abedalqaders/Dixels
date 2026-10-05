using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement.Tests.TestFixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using AbpUser = Volo.Abp.Identity.IdentityUser;

namespace Dixels.Users;

/// <summary>
/// My profile's password checklist (frontend passwordRules.ts) repeats what ASP.NET Identity's
/// PasswordValidator checks on the server. The shared cases go through the real validator
/// here and through the checklist in Vitest, so the two can't drift apart.
/// </summary>
public class PasswordRulesFixtureTests
{
    public record Rules(int RequiredLength, int RequiredUniqueChars, bool RequireDigit, bool RequireLowercase, bool RequireUppercase, bool RequireNonAlphanumeric);

    public record PasswordRulesCase(string Description, string Password, Rules Rules, string[] Unmet);

    // Identity's error codes, as the checklist names the rules.
    private static readonly Dictionary<string, string> RuleByErrorCode = new()
    {
        ["PasswordTooShort"] = "length",
        ["PasswordRequiresNonAlphanumeric"] = "symbol",
        ["PasswordRequiresDigit"] = "digit",
        ["PasswordRequiresLower"] = "lowercase",
        ["PasswordRequiresUpper"] = "uppercase",
        ["PasswordRequiresUniqueChars"] = "uniqueChars",
    };

    public static IEnumerable<object[]> Cases() =>
        SharedFixtureLoader.Load<PasswordRulesCase>("password-rules-cases.json").Select(c => new object[] { c.Description, c });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Identity_finds_the_same_unmet_rules(string description, PasswordRulesCase c)
    {
        var options = new IdentityOptions();
        options.Password.RequiredLength = c.Rules.RequiredLength;
        options.Password.RequiredUniqueChars = c.Rules.RequiredUniqueChars;
        options.Password.RequireDigit = c.Rules.RequireDigit;
        options.Password.RequireLowercase = c.Rules.RequireLowercase;
        options.Password.RequireUppercase = c.Rules.RequireUppercase;
        options.Password.RequireNonAlphanumeric = c.Rules.RequireNonAlphanumeric;

        // The validator only reads the manager's options and error describer.
        var manager = new UserManager<AbpUser>(
            Substitute.For<IUserStore<AbpUser>>(), Options.Create(options), null!, null!, null!, null!,
            new IdentityErrorDescriber(), null!, Substitute.For<ILogger<UserManager<AbpUser>>>());

        var result = await new PasswordValidator<AbpUser>().ValidateAsync(manager, new AbpUser(Guid.NewGuid(), "u", "u@test.io"), c.Password);

        result.Errors.Select(e => RuleByErrorCode[e.Code]).OrderBy(r => r).ShouldBe(c.Unmet.OrderBy(r => r), description);
    }
}
