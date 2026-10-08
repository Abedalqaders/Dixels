using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Bookings;
using Dixels.SpaceManagement;
using Dixels.Users;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Timing;

namespace Dixels.Data;

/* Demo data for trying the employee side locally: more floors and rooms in Riverside HQ,
 * the three seeded employees assigned to it, and a spread of bookings over the next few
 * days so Find a space has busy rooms to show.
 *
 * Runs only when the DbMigrator asks for it (EnabledPropertyName) — the test suites run
 * every seed contributor too, and bookings/assignments appearing there would break tests
 * that count rows. Everything is idempotent: floors and rooms are found by name, employees
 * already given a building by an admin are left alone, and each booking has a fixed
 * idempotency key per day, so re-running the migrator tops up the coming days instead of
 * duplicating. A floor or room an admin deleted stays deleted — it isn't recreated, and
 * its bookings are skipped. Bookings go through BookingManager, so every rule is enforced; one that no
 * longer fits (in the past, clashes with a real booking, a closed day) is simply skipped. */
public class RiversideDemoDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string EnabledPropertyName = "Dixels:DemoData";

    private const string BuildingName = "Riverside HQ";

    // The demo's names are English (the default language); an admin can add Arabic ones.
    private const string English = "en";
    private const int DaysAhead = 5;

    private readonly SpaceManagementHierarchyDataSeedContributor _hierarchySeeder;
    private readonly EmployeeUserDataSeedContributor _employeeSeeder;
    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly ISpaceTypeRepository _spaceTypeRepository;
    private readonly IRepository<AvailabilityOverride, Guid> _overrideRepository;
    private readonly IdentityUserManager _userManager;
    private readonly BookingManager _bookingManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IClock _clock;
    private readonly IDataFilter _dataFilter;

    public RiversideDemoDataSeedContributor(
        SpaceManagementHierarchyDataSeedContributor hierarchySeeder,
        EmployeeUserDataSeedContributor employeeSeeder,
        IRepository<Building, Guid> buildingRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Space, Guid> spaceRepository,
        ISpaceTypeRepository spaceTypeRepository,
        IRepository<AvailabilityOverride, Guid> overrideRepository,
        IdentityUserManager userManager,
        BookingManager bookingManager,
        IGuidGenerator guidGenerator,
        IClock clock,
        IDataFilter dataFilter)
    {
        _hierarchySeeder = hierarchySeeder;
        _employeeSeeder = employeeSeeder;
        _buildingRepository = buildingRepository;
        _floorRepository = floorRepository;
        _spaceRepository = spaceRepository;
        _spaceTypeRepository = spaceTypeRepository;
        _overrideRepository = overrideRepository;
        _userManager = userManager;
        _bookingManager = bookingManager;
        _guidGenerator = guidGenerator;
        _clock = clock;
        _dataFilter = dataFilter;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?[EnabledPropertyName] is not true)
        {
            return;
        }

        // Contributor order isn't guaranteed, so make sure the building and the employees
        // exist first. Both are idempotent.
        await _hierarchySeeder.SeedAsync(context);
        await _employeeSeeder.SeedAsync(context);

        var building = await _buildingRepository.FirstOrDefaultAsync(b => b.Translations.Any(t => t.Language == English && t.Name == BuildingName));
        if (building is null)
        {
            return;
        }

        var spaces = await SeedSpacesAsync(building);
        await AssignEmployeesAsync(building.Id);
        await SeedBookingsAsync(building, spaces);
    }

    // Null where an admin deleted the room (or its floor) — its bookings are skipped.
    private sealed record DemoSpaces(
        Space? Room101, Space? Room102, Space? Room201, Space? Room202,
        Space? Room301, Space? Room302, Space? BoardRoom, Space? Pod301, Space? Pod302,
        Space? Training401, Space? Room402);

    // SeedAsync runs the hierarchy seeder first, which ensures the built-in types exist.
    private async Task<SpaceType> GetBuiltInAsync(BuiltInSpaceType builtIn)
    {
        return await _spaceTypeRepository.FindByNameAsync("en", builtIn.EnglishName)
            ?? throw new InvalidOperationException($"The built-in space type '{builtIn.EnglishName}' hasn't been seeded.");
    }

    private async Task<DemoSpaces> SeedSpacesAsync(Building building)
    {
        var meetingRoom = await GetBuiltInAsync(BuiltInSpaceTypes.MeetingRoom);
        var focusPod = await GetBuiltInAsync(BuiltInSpaceTypes.FocusPod);
        var desk = await GetBuiltInAsync(BuiltInSpaceTypes.Desk);

        var level1 = await GetOrCreateFloorAsync(building.Id, "Level 1", 1);
        var level2 = await GetOrCreateFloorAsync(building.Id, "Level 2", 2);
        var level3 = await GetOrCreateFloorAsync(building.Id, "Level 3", 3);
        var level4 = await GetOrCreateFloorAsync(building.Id, "Level 4", 4);

        var room101 = await GetOrCreateSpaceAsync(level1, "Meeting Room 101", meetingRoom.Id, 8);
        var room102 = await GetOrCreateSpaceAsync(level1, "Meeting Room 102", meetingRoom.Id, 6);
        await GetOrCreateSpaceAsync(level1, "Desk 11", desk.Id, 1);

        var room201 = await GetOrCreateSpaceAsync(level2, "Meeting Room 201", meetingRoom.Id, 12);
        var room202 = await GetOrCreateSpaceAsync(level2, "Meeting Room 202", meetingRoom.Id, 6);
        await GetOrCreateSpaceAsync(level2, "Desk 13", desk.Id, 1);

        var room301 = await GetOrCreateSpaceAsync(level3, "Meeting Room 301", meetingRoom.Id, 8);
        var room302 = await GetOrCreateSpaceAsync(level3, "Meeting Room 302", meetingRoom.Id, 4);
        var boardRoom = await GetOrCreateSpaceAsync(level3, "Board Room 3C", meetingRoom.Id, 16, space =>
        {
            space.SetMinAttendees(6);
            space.SetOwnMaxDuration(180); // board meetings run long
        });
        var pod301 = await GetOrCreateSpaceAsync(level3, "Focus Pod 3-01", focusPod.Id, 1);
        var pod302 = await GetOrCreateSpaceAsync(level3, "Focus Pod 3-02", focusPod.Id, 1, space =>
            space.SetOwnMaxDuration(60)); // short focus sessions, so more people get one
        await GetOrCreateSpaceAsync(level3, "Desk 31", desk.Id, 1);
        await GetOrCreateSpaceAsync(level3, "Desk 32", desk.Id, 1);

        var training401 = await GetOrCreateSpaceAsync(level4, "Training Room 401", meetingRoom.Id, 24, space =>
            space.SetMinAttendees(5));
        var room402 = await GetOrCreateSpaceAsync(level4, "Meeting Room 402", meetingRoom.Id, 10);
        await GetOrCreateSpaceAsync(level4, "Focus Pod 4-01", focusPod.Id, 1);

        if (pod302 is not null)
        {
            await SeedClosureAsync(building, pod302);
        }

        return new DemoSpaces(room101, room102, room201, room202, room301, room302, boardRoom, pod301, pod302, training401, room402);
    }

    // One closure a couple of days out, so a closed room shows up in the results. Added
    // once: keyed on the room + reason, not on the date (which moves with "today").
    private async Task SeedClosureAsync(Building building, Space pod)
    {
        if (await _overrideRepository.AnyAsync(o => o.Scope == OverrideScope.Space && o.ScopeId == pod.Id && o.ReasonCategory == ReasonCategory.Maintenance))
        {
            return;
        }

        var clock = new BuildingClock(building.Timezone);
        var day = clock.LocalDate(Now()).AddDays(2);
        await _overrideRepository.InsertAsync(new AvailabilityOverride(
            _guidGenerator.Create(), OverrideScope.Space, pod.Id,
            clock.StartOfLocalDay(day), clock.StartOfLocalDay(day.AddDays(1)),
            OverrideEffect.Closed, ReasonCategory.Maintenance, "Replacing the chair and desk lamp"));
    }

    // Only employees with no building yet — an admin's own assignment is never overridden.
    private async Task AssignEmployeesAsync(Guid buildingId)
    {
        foreach (var userName in new[] { "jordan.reed", "amira.hassan", "leo.tran" })
        {
            var user = await _userManager.FindByNameAsync(userName);
            if (user is null || user.GetBuildingId() is not null)
            {
                continue;
            }

            user.SetBuildingId(buildingId);
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                throw new AbpException(
                    $"Could not assign '{userName}' to {BuildingName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }

    private async Task SeedBookingsAsync(Building building, DemoSpaces s)
    {
        var jordan = await _userManager.FindByNameAsync("jordan.reed");
        var amira = await _userManager.FindByNameAsync("amira.hassan");
        var leo = await _userManager.FindByNameAsync("leo.tran");
        if (jordan is null || amira is null || leo is null)
        {
            return;
        }

        // A typical day — repeated for each of the next few days, shifted a little on
        // odd days so the week doesn't look copy-pasted. Each is for the booker alone (the head
        // count is the booker plus their guests), so the rooms with a minimum group aren't booked.
        var day = new (Space? Space, IdentityUser User, int Start, int End, string Title)[]
        {
            (s.Room101, amira, Hm(9, 0), Hm(10, 0), "Product stand-up"),
            (s.Room101, leo, Hm(13, 0), Hm(14, 30), "Design review"),
            (s.Room102, leo, Hm(10, 30), Hm(11, 30), "1:1 with Sara"),
            (s.Room201, amira, Hm(11, 0), Hm(13, 0), "Quarterly planning"),
            (s.Room202, jordan, Hm(15, 0), Hm(16, 0), "Sprint retro"),
            (s.Room301, leo, Hm(9, 30), Hm(11, 0), "Client call — Acme"),
            (s.Room301, amira, Hm(14, 0), Hm(15, 0), "Hiring sync"),
            (s.Room302, jordan, Hm(12, 0), Hm(12, 45), "Lunch & learn prep"),
            (s.Pod301, leo, Hm(8, 0), Hm(9, 30), "Deep work"),
            (s.Pod302, amira, Hm(16, 0), Hm(17, 0), "Focus time"),
            (s.Room402, jordan, Hm(9, 0), Hm(10, 30), "Roadmap walkthrough"),
        };

        var clock = new BuildingClock(building.Timezone);
        var today = clock.LocalDate(Now());

        for (var offset = 0; offset < DaysAhead; offset++)
        {
            var date = today.AddDays(offset);
            var shift = offset % 2 == 1 ? 30 : 0;

            for (var i = 0; i < day.Length; i++)
            {
                var (space, user, start, end, title) = day[i];
                if (space is null)
                {
                    continue;
                }

                await TryBookAsync(
                    user.Id, space.Id,
                    date.ToDateTime(TimeOnly.MinValue).AddMinutes(start + shift),
                    date.ToDateTime(TimeOnly.MinValue).AddMinutes(end + shift),
                    title,
                    $"demo:{date:yyyyMMdd}:{i:00}");
            }
        }
    }

    private async Task TryBookAsync(Guid userId, Guid spaceId, DateTime localStart, DateTime localEnd, string title, string key)
    {
        try
        {
            await _bookingManager.CreateAsync(userId, spaceId, localStart, localEnd, Array.Empty<Invitee>(), title, key);
        }
        catch (BusinessException)
        {
            // Doesn't fit any more (already started, clashes with a real booking, closed day,
            // or the demo schedule changed since this key was used) — skip it.
        }
    }

    // Looked up with deleted rows included, so one an admin deleted is found (and left
    // alone, returned as null) instead of quietly recreated under the same name.
    private async Task<Floor?> GetOrCreateFloorAsync(Guid buildingId, string name, int floorNumber)
    {
        List<Floor> matches;
        using (_dataFilter.Disable<ISoftDelete>())
        {
            matches = await _floorRepository.GetListAsync(f => f.BuildingId == buildingId && f.Translations.Any(t => t.Language == English && t.Name == name));
        }

        if (matches.Count > 0)
        {
            // A live one wins (an admin may have deleted it and made a new one); only deleted ones → skip.
            return matches.FirstOrDefault(m => !m.IsDeleted);
        }

        return await _floorRepository.InsertAsync(new Floor(_guidGenerator.Create(), buildingId, English, name, floorNumber), autoSave: true);
    }

    private async Task<Space?> GetOrCreateSpaceAsync(Floor? floor, string name, Guid spaceTypeId, int capacity, Action<Space>? configure = null)
    {
        if (floor is null)
        {
            return null;
        }

        List<Space> matches;
        using (_dataFilter.Disable<ISoftDelete>())
        {
            matches = await _spaceRepository.GetListAsync(sp => sp.FloorId == floor.Id && sp.Translations.Any(t => t.Language == English && t.Name == name));
        }

        if (matches.Count > 0)
        {
            // A live one wins (an admin may have deleted it and made a new one); only deleted ones → skip.
            return matches.FirstOrDefault(m => !m.IsDeleted);
        }

        var space = new Space(_guidGenerator.Create(), floor.Id, English, name, spaceTypeId, capacity);
        configure?.Invoke(space);
        return await _spaceRepository.InsertAsync(space, autoSave: true);
    }

    private DateTimeOffset Now() => new(_clock.Now.ToUniversalTime(), TimeSpan.Zero);

    private static int Hm(int hours, int minutes) => hours * 60 + minutes;
}
