using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Dixels.Users;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;
using static Dixels.TestNames;

namespace Dixels.EntityFrameworkCore.Bookings;

/// <summary>
/// Changes that leave bookings behind without touching a room: a building's timezone
/// changing under them, and the person who booked leaving, being deactivated, or moving to
/// another building.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class PeopleAndTimezoneImpactTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBookingsAppService _bookings;
    private readonly IBuildingsAppService _buildings;
    private readonly IUsersAppService _users;
    private readonly IIdentityUserAppService _identityUsers;
    private readonly IBookingRepository _bookingRepository;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    private static readonly DateTime Tomorrow = DateTime.UtcNow.Date.AddDays(1);
    private static readonly Guid Admin = Guid.NewGuid();

    public PeopleAndTimezoneImpactTests()
    {
        _bookings = GetRequiredService<IBookingsAppService>();
        _buildings = GetRequiredService<IBuildingsAppService>();
        _users = GetRequiredService<IUsersAppService>();
        _identityUsers = GetRequiredService<IIdentityUserAppService>();
        _bookingRepository = GetRequiredService<IBookingRepository>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private sealed record Scenario(Guid UserId, Building Building, Space Space);

    private Task<Building> CreateBuildingAsync(string name) => WithUnitOfWorkAsync(() =>
        GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(
            Guid.NewGuid(), "en", name + " " + Guid.NewGuid().ToString("N")[..6], null, "UTC",
            new OperatingDays(OperatingDays.AllDaysMask), OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(20, 0)),
            maxDurationMinutes: 180, maxHorizonDays: 30, minLeadMinutes: 0)));

    /// <summary>A building with one room, and an employee assigned to it.</summary>
    private async Task<Scenario> CreateScenarioAsync()
    {
        var building = await CreateBuildingAsync("People HQ");
        return await WithUnitOfWorkAsync(async () =>
        {
            var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "Level 1", 1));
            var spaceType = await GetRequiredService<IRepository<SpaceType, Guid>>().FirstAsync();
            var space = await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(new Space(Guid.NewGuid(), floor.Id, "en", "Room 1", spaceType.Id, 8));

            var user = new IdentityUser(Guid.NewGuid(), "emp" + Guid.NewGuid().ToString("N")[..8], $"{Guid.NewGuid():N}@test.io")
            {
                Name = "Jordan",
                Surname = "Reed",
            };
            user.SetBuildingId(building.Id);
            (await GetRequiredService<IdentityUserManager>().CreateAsync(user, "1q2w3E*")).Succeeded.ShouldBeTrue();

            return new Scenario(user.Id, building, space);
        });
    }

    private IDisposable ActAs(Guid userId) =>
        _principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.UserId, userId.ToString()) })));

    private async Task<BookingDto> BookAsync(Scenario s, int startHour, int endHour)
    {
        using var _ = ActAs(s.UserId);
        return await _bookings.CreateAsync(new CreateBookingDto
        {
            SpaceId = s.Space.Id,
            LocalStart = Tomorrow.AddHours(startHour),
            LocalEnd = Tomorrow.AddHours(endHour),
            Attendees = 2,
            IdempotencyKey = Guid.NewGuid().ToString(),
        });
    }

    private Task<Booking> StoredAsync(Guid id) => WithUnitOfWorkAsync(() => _bookingRepository.GetAsync(id));

    private async Task<IdentityUserUpdateDto> UpdateFor(Guid userId, bool isActive)
    {
        var user = await _identityUsers.GetAsync(userId);
        var update = new IdentityUserUpdateDto
        {
            UserName = user.UserName,
            Email = user.Email,
            Name = user.Name,
            Surname = user.Surname,
            IsActive = isActive,
            LockoutEnabled = user.LockoutEnabled,
            ConcurrencyStamp = user.ConcurrencyStamp,
        };
        foreach (var (key, value) in user.ExtraProperties)
        {
            update.ExtraProperties[key] = value;
        }

        return update;
    }

    // ---- 1. Timezone ----

    [Fact]
    public async Task A_building_timezone_cannot_change_while_bookings_are_coming_up()
    {
        var s = await CreateScenarioAsync();
        await BookAsync(s, 10, 11);

        using var _ = ActAs(Admin);
        var ex = await Should.ThrowAsync<BusinessException>(() => _buildings.UpdateAsync(s.Building.Id, new UpdateBuildingDto
        {
            Names = En(s.Building.FindName("en")!),
            Timezone = "Asia/Amman",
        }));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.TimezoneChangeWithBookings);
        ex.Data["count"].ShouldBe(1);
        (await _buildings.GetAsync(s.Building.Id)).Timezone.ShouldBe("UTC");
    }

    [Fact]
    public async Task With_nothing_booked_ahead_the_timezone_can_change_and_the_name_always_can()
    {
        var s = await CreateScenarioAsync();

        using (ActAs(Admin))
        {
            (await _buildings.UpdateAsync(s.Building.Id, new UpdateBuildingDto { Names = En("Renamed"), Timezone = "Asia/Amman" }))
                .Timezone.ShouldBe("Asia/Amman");
        }

        var busy = await CreateScenarioAsync();
        await BookAsync(busy, 10, 11);
        using (ActAs(Admin))
        {
            (await _buildings.UpdateAsync(busy.Building.Id, new UpdateBuildingDto { Names = En("Renamed too"), Timezone = "UTC" }))
                .Name.ShouldBe("Renamed too");
        }
    }

    // ---- 2. The person leaves ----

    [Fact]
    public async Task Deactivating_an_account_releases_its_upcoming_bookings()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, 11);

        using var _ = ActAs(Admin);
        await _identityUsers.UpdateAsync(s.UserId, await UpdateFor(s.UserId, isActive: false));

        var stored = await StoredAsync(booking.Id);
        stored.Status.ShouldBe(BookingStatus.Cancelled);
        stored.CancelledByAdmin.ShouldBeTrue();
        stored.CancelReason.ShouldBe("The account was deactivated");
    }

    [Fact]
    public async Task Bookings_listen_for_the_announcement_not_the_user_service()
    {
        // Anything that deactivates an account (a future import, another module) only has to
        // announce it; Bookings releases the rooms itself.
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, 11);

        using var _ = ActAs(Admin);
        await WithUnitOfWorkAsync(() =>
            GetRequiredService<ILocalEventBus>().PublishAsync(new UserDeactivatedEvent(s.UserId, Admin)));

        var stored = await StoredAsync(booking.Id);
        stored.Status.ShouldBe(BookingStatus.Cancelled);
        stored.CancelledById.ShouldBe(Admin);
    }

    [Fact]
    public async Task Other_account_edits_leave_bookings_alone()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, 11);

        using var _ = ActAs(Admin);
        var update = await UpdateFor(s.UserId, isActive: true);
        update.Surname = "Reed-Hassan";
        await _identityUsers.UpdateAsync(s.UserId, update);

        (await StoredAsync(booking.Id)).Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Deleting_an_account_releases_its_upcoming_bookings()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, 11);

        using var _ = ActAs(Admin);
        await _identityUsers.DeleteAsync(s.UserId);

        (await StoredAsync(booking.Id)).CancelReason.ShouldBe("The account was removed");
    }

    // ---- 3. The person moves to another building ----

    [Fact]
    public async Task Moving_someone_lists_their_bookings_in_the_old_building_first()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, 11);

        using var _ = ActAs(Admin);
        var impact = await _users.GetReassignImpactAsync(s.UserId);
        impact.Bookings.ShouldHaveSingleItem().BookingId.ShouldBe(booking.Id);
        impact.Bookings[0].BookedBy.ShouldBe("Jordan Reed");
    }

    [Fact]
    public async Task Moving_someone_cancels_what_they_had_in_the_old_building()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, 11);
        var elsewhere = await CreateBuildingAsync("Annex");

        using (ActAs(Admin))
        {
            await _users.AssignBuildingAsync(s.UserId, new AssignUserBuildingDto { BuildingId = elsewhere.Id });
            (await _users.GetReassignImpactAsync(s.UserId)).Count.ShouldBe(0); // nothing booked in the new one
        }

        var stored = await StoredAsync(booking.Id);
        stored.CancelledByAdmin.ShouldBeTrue();
        stored.CancelReason.ShouldBe("Moved to another building");
        using (ActAs(s.UserId))
        {
            (await GetRequiredService<IAvailabilityAppService>().GetMyBuildingAsync()).ShouldNotBeNull().Id.ShouldBe(elsewhere.Id);
        }
    }

    [Fact]
    public async Task Moving_someone_through_the_account_form_cancels_too()
    {
        var s = await CreateScenarioAsync();
        var booking = await BookAsync(s, 10, 11);
        var elsewhere = await CreateBuildingAsync("Annex");

        using (ActAs(Admin))
        {
            var update = await UpdateFor(s.UserId, isActive: true);
            update.ExtraProperties[DixelsUserConsts.BuildingIdPropertyName] = elsewhere.Id.ToString();
            await _identityUsers.UpdateAsync(s.UserId, update);
        }

        (await StoredAsync(booking.Id)).CancelReason.ShouldBe("Moved to another building");
    }

    [Fact]
    public async Task The_calendar_shows_only_the_current_building()
    {
        var s = await CreateScenarioAsync();
        var old = await BookAsync(s, 10, 11);
        var elsewhere = await CreateBuildingAsync("Annex");
        using (ActAs(Admin))
        {
            await _users.AssignBuildingAsync(s.UserId, new AssignUserBuildingDto { BuildingId = elsewhere.Id });
        }

        using (ActAs(s.UserId))
        {
            var mine = await _bookings.GetMineAsync(new GetMyBookingsInput { From = Tomorrow, To = Tomorrow.AddDays(1) });
            mine.Items.ShouldNotContain(b => b.Id == old.Id);
        }
    }

    [Fact]
    public async Task Assigning_to_a_deleted_building_is_refused()
    {
        var s = await CreateScenarioAsync();
        var gone = await CreateBuildingAsync("Gone");
        await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<Building, Guid>>().DeleteAsync(gone.Id));

        using var _ = ActAs(Admin);
        await Should.ThrowAsync<Volo.Abp.Domain.Entities.EntityNotFoundException>(
            () => _users.AssignBuildingAsync(s.UserId, new AssignUserBuildingDto { BuildingId = gone.Id }));
    }
}
