using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Localization;
using Xunit;
using static Dixels.TestNames;

namespace Dixels.EntityFrameworkCore.SpaceManagement;

/// <summary>
/// Buildings, floors and spaces are named once per language, like space types: the reader
/// sees their language's name, else the English one; search finds a name in any language;
/// lists sort by the name the reader sees.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class HierarchyNamesTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBuildingsAppService _buildings;
    private readonly IFloorsAppService _floors;
    private readonly ISpacesAppService _spaces;
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;

    public HierarchyNamesTests()
    {
        _buildings = GetRequiredService<IBuildingsAppService>();
        _floors = GetRequiredService<IFloorsAppService>();
        _spaces = GetRequiredService<ISpacesAppService>();
        _spaceTypeRepository = GetRequiredService<IRepository<SpaceType, Guid>>();
    }

    private Task<BuildingDto> CreateBuildingAsync(System.Collections.Generic.List<Dixels.Localization.LocalizedNameDto> names) =>
        _buildings.CreateAsync(new CreateBuildingDto
        {
            Names = names,
            Timezone = "UTC",
            Days = new[] { 0, 1, 2, 3, 4, 5, 6 },
            Hours = new OperatingWindowDto { IsOpen24Hours = true },
            MaxDurationMinutes = 120,
            MaxHorizonDays = 30,
            MinLeadMinutes = 0,
        });

    private static async Task<T> InAsync<T>(string culture, Func<Task<T>> read)
    {
        using (CultureHelper.Use(culture))
        {
            return await read();
        }
    }

    [Fact]
    public async Task A_building_shows_its_name_in_the_readers_language_and_falls_back_to_English()
    {
        var both = await CreateBuildingAsync(EnAr("North Tower", "البرج الشمالي"));
        var englishOnly = await CreateBuildingAsync(En("South Tower"));

        (await InAsync("ar", () => _buildings.GetAsync(both.Id))).Name.ShouldBe("البرج الشمالي");
        (await InAsync("en", () => _buildings.GetAsync(both.Id))).Name.ShouldBe("North Tower");
        (await InAsync("ar", () => _buildings.GetAsync(englishOnly.Id))).Name.ShouldBe("South Tower");
        both.Names.Select(n => (n.Language, n.Name)).ShouldBe(new[] { ("ar", "البرج الشمالي"), ("en", "North Tower") });
    }

    [Fact]
    public async Task A_building_needs_an_English_name()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() =>
            CreateBuildingAsync([new() { Language = "ar", Name = "البرج" }]));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.DefaultLanguageNameRequired);
    }

    [Fact]
    public async Task Updating_a_building_adds_and_removes_names()
    {
        var created = await CreateBuildingAsync(En("Annex"));

        var withArabic = await _buildings.UpdateAsync(created.Id, new UpdateBuildingDto { Names = EnAr("Annex", "الملحق"), Timezone = "UTC" });
        withArabic.Names.Count.ShouldBe(2);

        var withoutArabic = await _buildings.UpdateAsync(created.Id, new UpdateBuildingDto { Names = En("Annex B"), Timezone = "UTC" });
        withoutArabic.Names.ShouldHaveSingleItem().Name.ShouldBe("Annex B");
    }

    [Fact]
    public async Task The_building_list_finds_a_name_in_any_language_and_sorts_by_the_one_shown()
    {
        var zeta = await CreateBuildingAsync(EnAr("Zeta House " + Tag, "أ بيت " + Tag));
        var alpha = await CreateBuildingAsync(EnAr("Alpha House " + Tag, "ي بيت " + Tag));

        // Search in Arabic while reading English, ignoring case in English.
        var byArabic = await InAsync("en", () => _buildings.GetListAsync(new GetBuildingsInput { Filter = "بيت " + Tag }));
        byArabic.Items.Select(b => b.Id).ShouldBe(new[] { alpha.Id, zeta.Id });
        (await _buildings.GetListAsync(new GetBuildingsInput { Filter = "zeta HOUSE " + Tag })).Items.ShouldHaveSingleItem().Id.ShouldBe(zeta.Id);

        // In Arabic, sorted by the Arabic names: أ before ي.
        var inArabic = await InAsync("ar", () => _buildings.GetListAsync(new GetBuildingsInput { Filter = Tag }));
        inArabic.Items.Select(b => b.Id).ShouldBe(new[] { zeta.Id, alpha.Id });
        inArabic.Items[0].Name.ShouldBe("أ بيت " + Tag);
    }

    [Fact]
    public async Task Floor_and_space_lists_show_every_level_in_the_readers_language()
    {
        var building = await CreateBuildingAsync(EnAr("East Wing", "الجناح الشرقي"));
        var floor = await _floors.CreateAsync(new CreateFloorDto { BuildingId = building.Id, Names = EnAr("Level 2", "الطابق الثاني"), FloorNumber = 2 });
        var spaceType = await WithUnitOfWorkAsync(() => _spaceTypeRepository.FirstAsync());
        var space = await _spaces.CreateAsync(new CreateSpaceDto
        {
            FloorId = floor.Id,
            Names = EnAr("Room 201", "غرفة 201"),
            SpaceTypeId = spaceType.Id,
            Capacity = 4,
        });

        var floors = await InAsync("ar", () => _floors.GetListAsync(new GetFloorsInput { BuildingId = building.Id }));
        var floorRow = floors.Items.ShouldHaveSingleItem();
        floorRow.Name.ShouldBe("الطابق الثاني");
        floorRow.BuildingName.ShouldBe("الجناح الشرقي");
        floorRow.Names.Count.ShouldBe(2);

        var spaces = await InAsync("ar", () => _spaces.GetListAsync(new GetSpacesInput { FloorId = floor.Id }));
        var spaceRow = spaces.Items.ShouldHaveSingleItem();
        (spaceRow.Name, spaceRow.FloorName, spaceRow.BuildingName).ShouldBe(("غرفة 201", "الطابق الثاني", "الجناح الشرقي"));

        // The search matches the building's Arabic name from the spaces list too.
        (await _spaces.GetListAsync(new GetSpacesInput { Filter = "الجناح الشرقي" })).Items.ShouldContain(s => s.Id == space.Id);

        (await InAsync("en", () => _spaces.GetAsync(space.Id))).Name.ShouldBe("Room 201");
    }

    // Keeps each test's rows apart from the seeded ones in searches. Capitals, so it's a code
    // an Arabic name may hold too (NameAlphabet).
    private static readonly string Tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
}
