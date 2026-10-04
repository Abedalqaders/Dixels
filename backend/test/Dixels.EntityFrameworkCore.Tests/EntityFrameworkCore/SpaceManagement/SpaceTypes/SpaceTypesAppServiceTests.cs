using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Localization;
using Volo.Abp.Validation;
using Xunit;

namespace Dixels.EntityFrameworkCore.SpaceManagement;

[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class SpaceTypesAppServiceTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly ISpaceTypesAppService _spaceTypesAppService;
    private readonly ISpaceTypeRepository _spaceTypeRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IRepository<Floor, Guid> _floorRepository;
    private readonly IRepository<Building, Guid> _buildingRepository;

    public SpaceTypesAppServiceTests()
    {
        _spaceTypesAppService = GetRequiredService<ISpaceTypesAppService>();
        _spaceTypeRepository = GetRequiredService<ISpaceTypeRepository>();
        _spaceRepository = GetRequiredService<IRepository<Space, Guid>>();
        _floorRepository = GetRequiredService<IRepository<Floor, Guid>>();
        _buildingRepository = GetRequiredService<IRepository<Building, Guid>>();
    }

    /// <summary>Names as the form sends them: ("en", "Desk"), ("ar", "مكتب")…</summary>
    private static List<SpaceTypeNameDto> Names(params (string Language, string Name)[] names) =>
        names.Select(n => new SpaceTypeNameDto { Language = n.Language, Name = n.Name }).ToList();

    private Task<SpaceTypeDto> CreateAsync(params (string Language, string Name)[] names) =>
        _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto { Names = Names(names), IconKey = IconKey.Generic });

    private async Task<SpaceTypeDto> GetListItemAsync(Guid id, string culture)
    {
        using (CultureHelper.Use(culture))
        {
            return (await _spaceTypesAppService.GetListAsync()).Items.Single(t => t.Id == id);
        }
    }

    [Fact]
    public async Task Create_Then_GetList_Returns_The_New_SpaceType()
    {
        var created = await _spaceTypesAppService.CreateAsync(new CreateSpaceTypeDto
        {
            Names = Names(("en", "Phone Booth")),
            IconKey = IconKey.FocusPod
        });

        var list = await _spaceTypesAppService.GetListAsync();

        list.Items.ShouldContain(t => t.Id == created.Id && t.Name == "Phone Booth" && t.IconKey == IconKey.FocusPod);
    }

    // ---- which name a reader sees ----

    [Fact]
    public async Task The_Name_Shown_Is_In_The_Readers_Language()
    {
        var created = await CreateAsync(("en", "Phone booth"), ("ar", "كابينة هاتف"));

        (await GetListItemAsync(created.Id, "ar")).Name.ShouldBe("كابينة هاتف");
        (await GetListItemAsync(created.Id, "en")).Name.ShouldBe("Phone booth");
    }

    [Fact]
    public async Task A_Regional_Language_Uses_Its_Parent_Languages_Name()
    {
        var created = await CreateAsync(("en", "Phone booth"), ("ar", "كابينة هاتف"));

        (await GetListItemAsync(created.Id, "ar-JO")).Name.ShouldBe("كابينة هاتف");
    }

    [Fact]
    public async Task Without_A_Name_In_The_Readers_Language_The_English_One_Is_Shown()
    {
        var created = await CreateAsync(("en", "Quiet room"));

        (await GetListItemAsync(created.Id, "ar")).Name.ShouldBe("Quiet room");
    }

    [Fact]
    public async Task Every_Name_Comes_Back_For_The_Edit_Form()
    {
        var created = await CreateAsync(("en", "Phone booth"), ("ar", "كابينة هاتف"));

        var item = await GetListItemAsync(created.Id, "ar");

        item.Names.Select(n => (n.Language, n.Name)).ShouldBe(new[] { ("ar", "كابينة هاتف"), ("en", "Phone booth") });
    }

    [Fact]
    public async Task The_List_Is_Sorted_By_The_Name_Shown()
    {
        await CreateAsync(("en", "Zz last in English"), ("ar", "أأ أول بالعربية"));

        using (CultureHelper.Use("ar"))
        {
            var names = (await _spaceTypesAppService.GetListAsync()).Items.Select(t => t.Name).ToList();
            names.ShouldBe(names.OrderBy(n => n, StringComparer.Create(new System.Globalization.CultureInfo("ar"), ignoreCase: true)).ToList());
        }
    }

    // ---- what names are accepted ----

    [Fact]
    public async Task The_Default_Language_Name_Is_Required()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => CreateAsync(("ar", "كابينة فقط")));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.DefaultLanguageNameRequired);
    }

    [Fact]
    public async Task A_Blank_Name_Is_Invalid_Input()
    {
        // The form never sends one (an empty row is left out); the API refuses it outright.
        await Should.ThrowAsync<AbpValidationException>(() => CreateAsync(("en", "   "), ("ar", "كابينة")));
    }

    [Fact]
    public async Task A_Language_The_App_Does_Not_Offer_Is_Rejected()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => CreateAsync(("en", "Phone booth"), ("xx", "Something")));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.UnsupportedLanguage);
        ex.Data["language"].ShouldBe("xx");
    }

    [Fact]
    public async Task A_Language_Listed_Twice_Is_Rejected()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => CreateAsync(("en", "Phone booth"), ("ar", "كابينة"), ("ar", "كشك")));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.LanguageListedTwice);
    }

    [Fact]
    public async Task Names_Are_Saved_Trimmed()
    {
        var created = await CreateAsync(("en", "  Phone booth  "));

        created.Name.ShouldBe("Phone booth");
    }

    // ---- unique per language ----

    [Fact]
    public async Task The_Same_Name_Ignoring_Case_And_Spaces_Is_A_Duplicate()
    {
        await CreateAsync(("en", "Desk lamp"));

        var ex = await Should.ThrowAsync<BusinessException>(() => CreateAsync(("en", "desk LAMP ")));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.SpaceTypeNameAlreadyExists);
        ex.Data["language"].ShouldBe("en");
    }

    [Fact]
    public async Task A_Duplicate_Arabic_Name_Names_Its_Language()
    {
        await CreateAsync(("en", "Phone booth"), ("ar", "كابينة هاتف"));

        var ex = await Should.ThrowAsync<BusinessException>(() => CreateAsync(("en", "Call booth"), ("ar", "كابينة هاتف")));

        ex.Code.ShouldBe(DixelsDomainErrorCodes.SpaceTypeNameAlreadyExists);
        ex.Data["language"].ShouldBe("ar");
        ex.Data["name"].ShouldBe("كابينة هاتف");
    }

    [Fact]
    public async Task The_Same_Text_In_Different_Languages_Is_Not_A_Duplicate()
    {
        await CreateAsync(("en", "Lounge"));

        // Another type whose Arabic name happens to be the same word.
        var other = await CreateAsync(("en", "Lounge area"), ("ar", "Lounge"));

        other.Names.ShouldContain(n => n.Language == "ar" && n.Name == "Lounge");
    }

    [Fact]
    public async Task Update_To_Another_SpaceTypes_Name_Throws()
    {
        await CreateAsync(("en", "Taken"));
        var other = await CreateAsync(("en", "Free"));

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            _spaceTypesAppService.UpdateAsync(other.Id, new UpdateSpaceTypeDto { Names = Names(("en", "Taken")) }));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.SpaceTypeNameAlreadyExists);
    }

    [Fact]
    public async Task Update_Keeping_Its_Own_Name_Does_Not_Throw()
    {
        var created = await CreateAsync(("en", "Stays The Same"));

        var updated = await _spaceTypesAppService.UpdateAsync(created.Id, new UpdateSpaceTypeDto
        {
            Names = Names(("en", "stays the same")),
            IconKey = IconKey.Desk
        });

        updated.IconKey.ShouldBe(IconKey.Desk);
        updated.Name.ShouldBe("stays the same");
    }

    // ---- editing ----

    [Fact]
    public async Task Update_Adds_Changes_And_Removes_Names()
    {
        var created = await CreateAsync(("en", "Old name"));

        var added = await _spaceTypesAppService.UpdateAsync(created.Id, new UpdateSpaceTypeDto
        {
            Names = Names(("en", "New name"), ("ar", "اسم جديد")),
            IconKey = IconKey.MeetingRoom
        });
        added.Names.Select(n => (n.Language, n.Name)).ShouldBe(new[] { ("ar", "اسم جديد"), ("en", "New name") });
        added.IconKey.ShouldBe(IconKey.MeetingRoom);

        var removed = await _spaceTypesAppService.UpdateAsync(created.Id, new UpdateSpaceTypeDto
        {
            Names = Names(("en", "New name")),
            IconKey = IconKey.MeetingRoom
        });
        removed.Names.Select(n => (n.Language, n.Name)).ShouldBe(new[] { ("en", "New name") });

        // And it's what's stored, not just what came back.
        (await GetListItemAsync(created.Id, "ar")).Name.ShouldBe("New name");
    }

    // ---- deleting ----

    [Fact]
    public async Task Delete_Not_In_Use_Succeeds()
    {
        var created = await CreateAsync(("en", "Unused Type"));

        await _spaceTypesAppService.DeleteAsync(created.Id);

        var list = await _spaceTypesAppService.GetListAsync();
        list.Items.ShouldNotContain(t => t.Id == created.Id);
    }

    [Fact]
    public async Task Delete_When_In_Use_By_A_Space_Throws()
    {
        var spaceType = await CreateAsync(("en", "In Use Type"));
        await CreateSpaceUsingSpaceTypeAsync(spaceType.Id);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            _spaceTypesAppService.DeleteAsync(spaceType.Id));

        exception.Code.ShouldBe(DixelsDomainErrorCodes.SpaceTypeInUse);
    }

    [Fact]
    public async Task Deleted_SpaceTypes_Names_Can_Be_Reused_In_Every_Language()
    {
        var first = await CreateAsync(("en", "Reusable Name"), ("ar", "اسم قابل لإعادة الاستخدام"));
        await _spaceTypesAppService.DeleteAsync(first.Id);

        // Proves the filtered unique index (WHERE "IsDeleted" = false on the names) through
        // the actual application service, not just a raw SQL check against the table.
        var second = await CreateAsync(("en", "Reusable Name"), ("ar", "اسم قابل لإعادة الاستخدام"));

        second.Id.ShouldNotBe(first.Id);
    }

    [Fact]
    public async Task A_Deleted_SpaceType_Keeps_Its_Names_Marked_Deleted()
    {
        var created = await CreateAsync(("en", "Archived booth"), ("ar", "كابينة مؤرشفة"));

        await _spaceTypesAppService.DeleteAsync(created.Id);

        // Soft delete: the rows stay (a deleted space can still point at this type), only
        // flagged so the unique index ignores them.
        var rows = await WithUnitOfWorkAsync(async () =>
        {
            var dbContext = await GetRequiredService<IDbContextProvider<DixelsDbContext>>().GetDbContextAsync();
            return await dbContext.Set<SpaceTypeTranslation>().Where(t => t.SpaceTypeId == created.Id).ToListAsync();
        });
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(t => t.IsDeleted);
    }

    // ---- built-in types (the seeders) ----

    [Fact]
    public async Task Built_In_Types_Are_Seeded_In_English_And_Arabic()
    {
        var desk = await WithUnitOfWorkAsync(() => _spaceTypeRepository.FindByNameAsync("en", BuiltInSpaceTypes.Desk.EnglishName));

        desk.ShouldNotBeNull();
        desk.FindName("ar").ShouldBe(BuiltInSpaceTypes.Desk.ArabicName);
    }

    [Fact]
    public async Task Seeding_Again_Adds_No_Types_And_Keeps_An_Edited_Arabic_Name()
    {
        var desk = await WithUnitOfWorkAsync(() => _spaceTypeRepository.FindByNameAsync("en", BuiltInSpaceTypes.Desk.EnglishName));
        await _spaceTypesAppService.UpdateAsync(desk!.Id, new UpdateSpaceTypeDto
        {
            Names = Names(("en", BuiltInSpaceTypes.Desk.EnglishName), ("ar", "مكتب مشترك")),
            IconKey = IconKey.Desk
        });
        var countBefore = await WithUnitOfWorkAsync(() => _spaceTypeRepository.GetCountAsync());

        await GetRequiredService<IDataSeeder>().SeedAsync();
        await GetRequiredService<IDataSeeder>().SeedAsync();

        (await WithUnitOfWorkAsync(() => _spaceTypeRepository.GetCountAsync())).ShouldBe(countBefore);
        (await GetListItemAsync(desk.Id, "ar")).Name.ShouldBe("مكتب مشترك");
    }

    [Fact]
    public async Task Seeding_Adds_A_Missing_Arabic_Name_To_A_Built_In_Type()
    {
        var focusPod = await WithUnitOfWorkAsync(() => _spaceTypeRepository.FindByNameAsync("en", BuiltInSpaceTypes.FocusPod.EnglishName));
        // Like a database from before Arabic: only the English name.
        await _spaceTypesAppService.UpdateAsync(focusPod!.Id, new UpdateSpaceTypeDto
        {
            Names = Names(("en", BuiltInSpaceTypes.FocusPod.EnglishName)),
            IconKey = IconKey.FocusPod
        });

        await GetRequiredService<IDataSeeder>().SeedAsync();

        (await GetListItemAsync(focusPod.Id, "ar")).Name.ShouldBe(BuiltInSpaceTypes.FocusPod.ArabicName);
    }

    private async Task CreateSpaceUsingSpaceTypeAsync(Guid spaceTypeId)
    {
        var building = new Building(
            Guid.NewGuid(),
            "Test Building",
            null,
            "UTC",
            OperatingDays.Everyday,
            OperatingWindow.FullDay,
            maxDurationMinutes: 60,
            maxHorizonDays: 30,
            minLeadMinutes: 0);
        await _buildingRepository.InsertAsync(building);

        var floor = new Floor(Guid.NewGuid(), building.Id, "Test Floor", null);
        await _floorRepository.InsertAsync(floor);

        var space = new Space(Guid.NewGuid(), floor.Id, "Test Space", spaceTypeId, capacity: 4);
        await _spaceRepository.InsertAsync(space);
    }
}
