using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.EntityFrameworkCore.Users;

/// <summary>
/// My profile: ABP's own profile service, as Dixels configures it — people change their name
/// and phone, never what they sign in with or the building an admin gave them.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class MyProfileTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IProfileAppService _profileAppService;
    private readonly IIdentityUserAppService _identityUserAppService;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    public MyProfileTests()
    {
        _profileAppService = GetRequiredService<IProfileAppService>();
        _identityUserAppService = GetRequiredService<IIdentityUserAppService>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private Task<Building> CreateBuildingAsync() => WithUnitOfWorkAsync(() => _buildingRepository.InsertAsync(new Building(
        Guid.NewGuid(), "en", "HQ " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
        new OperatingDays(OperatingDays.AllDaysMask), new OperatingWindow(true, TimeOnly.MinValue, TimeOnly.MinValue),
        maxDurationMinutes: 120, maxHorizonDays: 30, minLeadMinutes: 0)));

    // Unique per test: the fixture's database is shared by every test in the run.
    private Task<IdentityUser> CreateUserAsync(Guid? buildingId = null) => WithUnitOfWorkAsync(async () =>
    {
        var tag = Guid.NewGuid().ToString("N")[..10];
        var user = new IdentityUser(Guid.NewGuid(), "u" + tag, $"{tag}@test.io") { Name = "Test", Surname = tag };
        user.SetBuildingId(buildingId);
        (await _userManager.CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();
        return user;
    });

    private Task<IdentityUser> StoredAsync(Guid userId) => WithUnitOfWorkAsync(() => _userManager.GetByIdAsync(userId));

    private IDisposable ActAs(Guid userId)
    {
        return _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AbpClaimTypes.UserId, userId.ToString()),
        })));
    }

    // What the profile page sends: the profile as it came back, with the edits on top.
    private static UpdateProfileDto EditOf(ProfileDto current) => new()
    {
        UserName = current.UserName,
        Email = current.Email,
        Name = current.Name,
        Surname = current.Surname,
        PhoneNumber = current.PhoneNumber,
        ConcurrencyStamp = current.ConcurrencyStamp,
    };

    [Fact]
    public async Task Saves_the_name_and_phone()
    {
        var user = await CreateUserAsync();
        using (ActAs(user.Id))
        {
            var input = EditOf(await _profileAppService.GetAsync());
            input.Name = "Sara";
            input.Surname = "Haddad";
            input.PhoneNumber = "+962 7 9000 0000";

            await _profileAppService.UpdateAsync(input);
        }

        var stored = await StoredAsync(user.Id);
        stored.Name.ShouldBe("Sara");
        stored.Surname.ShouldBe("Haddad");
        stored.PhoneNumber.ShouldBe("+962 7 9000 0000");
    }

    [Fact]
    public async Task Leaves_the_username_and_email_as_they_were()
    {
        var user = await CreateUserAsync();
        using (ActAs(user.Id))
        {
            var input = EditOf(await _profileAppService.GetAsync());
            input.UserName = "someone-else";
            input.Email = "someone-else@test.io";

            await _profileAppService.UpdateAsync(input);
        }

        var stored = await StoredAsync(user.Id);
        stored.UserName.ShouldBe(user.UserName);
        stored.Email.ShouldBe(user.Email);
    }

    [Fact]
    public async Task Leaves_the_building_an_admin_gave_them()
    {
        var mine = await CreateBuildingAsync();
        var other = await CreateBuildingAsync();
        var user = await CreateUserAsync(mine.Id);
        using (ActAs(user.Id))
        {
            var input = EditOf(await _profileAppService.GetAsync());
            input.ExtraProperties[DixelsUserConsts.BuildingIdPropertyName] = other.Id.ToString();

            await _profileAppService.UpdateAsync(input);
        }

        (await StoredAsync(user.Id)).GetBuildingId().ShouldBe(mine.Id);
    }

    [Fact]
    public async Task Changes_the_password()
    {
        var user = await CreateUserAsync();
        using (ActAs(user.Id))
        {
            await _profileAppService.ChangePasswordAsync(new ChangePasswordInput { CurrentPassword = "1q2w3E*", NewPassword = "N3w-pass!" });
        }

        var stored = await StoredAsync(user.Id);
        (await WithUnitOfWorkAsync(() => _userManager.CheckPasswordAsync(stored, "N3w-pass!"))).ShouldBeTrue();
    }

    // The profile page puts this one under the current-password box, by its code (ABP gives none).
    [Fact]
    public async Task Refuses_a_wrong_current_password_with_its_own_code()
    {
        var user = await CreateUserAsync();
        using (ActAs(user.Id))
        {
            var error = await Should.ThrowAsync<BusinessException>(() =>
                _profileAppService.ChangePasswordAsync(new ChangePasswordInput { CurrentPassword = "wrong", NewPassword = "N3w-pass!" }));
            error.Code.ShouldBe(DixelsDomainErrorCodes.WrongCurrentPassword);
        }
    }

    [Fact]
    public async Task An_admin_can_still_change_someones_email()
    {
        var user = await CreateUserAsync();
        var current = await _identityUserAppService.GetAsync(user.Id);

        await _identityUserAppService.UpdateAsync(user.Id, new IdentityUserUpdateDto
        {
            UserName = current.UserName,
            Email = "moved-" + current.Email,
            Name = current.Name,
            Surname = current.Surname,
            PhoneNumber = current.PhoneNumber,
            IsActive = current.IsActive,
            LockoutEnabled = current.LockoutEnabled,
            ConcurrencyStamp = current.ConcurrencyStamp,
        });

        (await StoredAsync(user.Id)).Email.ShouldBe("moved-" + current.Email);
    }
}
