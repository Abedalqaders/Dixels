using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Data;
using Dixels.SpaceManagement.ValueObjects;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Dixels.SpaceManagement;

/* Seeds a small but representative Building -> Floor -> Space hierarchy plus a few
 * AvailabilityOverrides, so the admin UI has real data to show instead of an empty state.
 * Cross-cutting (spans all four aggregates), so it lives at the SpaceManagement root rather
 * than under one aggregate's folder. Guarded by name ("Riverside HQ"), not "any Building
 * exists" — it runs alongside whatever else is already in the database instead of being
 * silently skipped by it, and is idempotent (won't recreate itself) either way.
 *
 * Sample data only: it runs when the DbMigrator is asked for demo data (Dixels:DemoData) or
 * a caller asks for just this hierarchy (EnabledPropertyName — the test suites do, without
 * the demo bookings). A production migration seeds none of it. */
public class SpaceManagementHierarchyDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    /// <summary>Seeds the sample buildings without the rest of the demo data (bookings, employees).</summary>
    public const string EnabledPropertyName = "Dixels:SampleHierarchy";

    private static readonly DayOfWeek[] Weekdays =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
    };

    // The seeded names are English (the default language); an admin can add Arabic ones.
    private const string English = "en";

    private readonly IRepository<Building, Guid> _buildingRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<AvailabilityOverride, Guid> _overrideRepository;
    private readonly SpaceTypeManager _spaceTypeManager;
    private readonly IGuidGenerator _guidGenerator;

    public SpaceManagementHierarchyDataSeedContributor(
        IRepository<Building, Guid> buildingRepository,
        IRepository<Floor, Guid> floorRepository,
        IRepository<Space, Guid> spaceRepository,
        IRepository<AvailabilityOverride, Guid> overrideRepository,
        SpaceTypeManager spaceTypeManager,
        IGuidGenerator guidGenerator)
    {
        _buildingRepository = buildingRepository;
        _floorRepository = floorRepository;
        _spaceRepository = spaceRepository;
        _overrideRepository = overrideRepository;
        _spaceTypeManager = spaceTypeManager;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?[EnabledPropertyName] is not true && context?[RiversideDemoDataSeedContributor.EnabledPropertyName] is not true)
        {
            return;
        }

        // Checked by name, not "any Building exists" — this seed data should land alongside
        // whatever an admin has already created through the UI, not be skipped because of it.
        if (await _buildingRepository.AnyAsync(b => b.Translations.Any(t => t.Language == English && t.Name == "Riverside HQ")))
        {
            return;
        }

        // Looked up by name rather than assumed to already exist — contributor execution
        // order across IDataSeedContributor implementations isn't guaranteed, so this can't
        // rely on SpaceTypeDataSeedContributor having run first.
        // The same built-in types SpaceTypeDataSeedContributor seeds (order isn't guaranteed).
        var meetingRoom = await _spaceTypeManager.EnsureBuiltInAsync(BuiltInSpaceTypes.MeetingRoom);
        var focusPod = await _spaceTypeManager.EnsureBuiltInAsync(BuiltInSpaceTypes.FocusPod);
        var desk = await _spaceTypeManager.EnsureBuiltInAsync(BuiltInSpaceTypes.Desk);

        // Each Building gets its own OperatingDays/OperatingWindow instance rather than the
        // OperatingDays.Everyday / OperatingWindow.FullDay singletons — EF Core can't track
        // the same owned-entity instance as belonging to two different aggregate roots in one
        // change-tracking session; sharing one here silently nulled the other's "Days" column
        // on save (23502 not-null violation on AppBuildings.Days).
        var riversideDays = EverydayDays();
        var riversideHours = OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(20, 0));

        var riverside = await CreateBuildingAsync(
            "Riverside HQ", "RH-01", "Asia/Amman",
            riversideDays, riversideHours,
            maxDurationMinutes: 120, maxHorizonDays: 60, minLeadMinutes: 15);

        var downtown = await CreateBuildingAsync(
            "Downtown Annex", "DA-02", "Europe/London",
            EverydayDays(), FullDayWindow(),
            maxDurationMinutes: 180, maxHorizonDays: 30, minLeadMinutes: 0);

        var level1 = await CreateFloorAsync(riverside.Id, "Level 1", 1);
        var level2 = await CreateFloorAsync(riverside.Id, "Level 2", 2);

        // Level 2 narrows the building's hours/days — gives the hierarchy a real "Custom"
        // badge to render, not just inherited defaults everywhere. Narrowed against the local
        // riversideDays/riversideHours instances (see the shared-singleton note above), not
        // riverside.Days/riverside.Hours.
        level2.SetOwnOperatingDays(OperatingDays.FromDayOfWeeks(Weekdays), riversideDays);
        level2.SetOwnOperatingHours(OperatingWindow.Create(new TimeOnly(8, 0), new TimeOnly(18, 0)), riversideHours);
        await _floorRepository.UpdateAsync(level2);

        var ground = await CreateFloorAsync(downtown.Id, "Ground Floor", 0);

        await CreateSpaceAsync(level1.Id, "Meeting Room 101", meetingRoom.Id, capacity: 8);
        await CreateSpaceAsync(level1.Id, "Focus Pod A", focusPod.Id, capacity: 1);

        var meetingRoom201 = await CreateSpaceAsync(level2.Id, "Meeting Room 201", meetingRoom.Id, capacity: 12);
        meetingRoom201.SetMinAttendees(4); // large room — avoid single-person bookings wasting it
        await _spaceRepository.UpdateAsync(meetingRoom201);

        await CreateSpaceAsync(level2.Id, "Desk 12", desk.Id, capacity: 1);

        await CreateSpaceAsync(ground.Id, "Meeting Room G1", meetingRoom.Id, capacity: 6);
        await CreateSpaceAsync(ground.Id, "Desk 1", desk.Id, capacity: 1);
        await CreateSpaceAsync(ground.Id, "Desk 2", desk.Id, capacity: 1);

        // One closure, one maintenance window, one special opening — covers both Effects,
        // all three Scopes, and three different ReasonCategory values.
        await CreateOverrideAsync(
            OverrideScope.Building, riverside.Id,
            new DateTimeOffset(2026, 12, 25, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 12, 26, 0, 0, 0, TimeSpan.Zero),
            OverrideEffect.Closed, ReasonCategory.Holiday, "Christmas Day");

        await CreateOverrideAsync(
            OverrideScope.Floor, level2.Id,
            new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.Zero),
            OverrideEffect.Closed, ReasonCategory.Maintenance, "HVAC maintenance");

        await CreateOverrideAsync(
            OverrideScope.Space, meetingRoom201.Id,
            new DateTimeOffset(2026, 10, 10, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 10, 20, 0, 0, TimeSpan.Zero),
            OverrideEffect.Open, ReasonCategory.Event, "Client conference — extended hours");
    }

    private static OperatingDays EverydayDays() => new(OperatingDays.AllDaysMask);

    private static OperatingWindow FullDayWindow() => new(isOpen24Hours: true, TimeOnly.MinValue, TimeOnly.MinValue);

    private async Task<Building> CreateBuildingAsync(
        string name, string buildingNumber, string timezone,
        OperatingDays days, OperatingWindow hours,
        int maxDurationMinutes, int maxHorizonDays, int minLeadMinutes)
    {
        return await _buildingRepository.InsertAsync(new Building(
            _guidGenerator.Create(), English, name, buildingNumber, timezone,
            days, hours, maxDurationMinutes, maxHorizonDays, minLeadMinutes));
    }

    private async Task<Floor> CreateFloorAsync(Guid buildingId, string name, int floorNumber)
    {
        return await _floorRepository.InsertAsync(new Floor(_guidGenerator.Create(), buildingId, English, name, floorNumber));
    }

    private async Task<Space> CreateSpaceAsync(Guid floorId, string name, Guid spaceTypeId, int capacity)
    {
        return await _spaceRepository.InsertAsync(new Space(_guidGenerator.Create(), floorId, English, name, spaceTypeId, capacity));
    }

    private async Task CreateOverrideAsync(
        OverrideScope scope, Guid scopeId, DateTimeOffset startsAt, DateTimeOffset endsAt,
        OverrideEffect effect, ReasonCategory reasonCategory, string reasonDetail)
    {
        await _overrideRepository.InsertAsync(new AvailabilityOverride(
            _guidGenerator.Create(), scope, scopeId, startsAt, endsAt, effect, reasonCategory, reasonDetail));
    }
}
