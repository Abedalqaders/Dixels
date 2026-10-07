using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Localization;
using Dixels.SpaceManagement;
using Shouldly;
using Xunit;
using static Dixels.TestNames;

namespace Dixels.EntityFrameworkCore.SpaceManagement;

/// <summary>
/// A building's street address is optional and given per language, next to its name in that
/// language: an admin adds, changes and clears it, and renaming the building keeps it.
/// </summary>
[Collection(DixelsTestConsts.CollectionDefinitionName)]
public class BuildingAddressTests : DixelsApplicationTestBase<DixelsEntityFrameworkCoreTestModule>
{
    private readonly IBuildingsAppService _buildings;

    public BuildingAddressTests()
    {
        _buildings = GetRequiredService<IBuildingsAppService>();
    }

    private Task<BuildingDto> CreateBuildingAsync(List<LocalizedNameDto> names, List<BuildingAddressDto> addresses) =>
        _buildings.CreateAsync(new CreateBuildingDto
        {
            Names = names,
            Addresses = addresses,
            Timezone = "UTC",
            Days = new[] { 0, 1, 2, 3, 4, 5, 6 },
            Hours = new OperatingWindowDto { IsOpen24Hours = true },
            MaxDurationMinutes = 120,
            MaxHorizonDays = 30,
            MinLeadMinutes = 0,
        });

    private Task<BuildingDto> UpdateAsync(BuildingDto building, List<LocalizedNameDto> names, List<BuildingAddressDto> addresses) =>
        _buildings.UpdateAsync(building.Id, new UpdateBuildingDto
        {
            Names = names,
            Addresses = addresses,
            BuildingNumber = building.BuildingNumber,
            Timezone = building.Timezone,
        });

    private static BuildingAddressDto Address(string language, string address) => new() { Language = language, Address = address };

    private static (string, string)[] Shown(BuildingDto building) =>
        building.Addresses.Select(a => (a.Language, a.Address)).ToArray();

    [Fact]
    public async Task A_building_is_created_with_an_address_per_language()
    {
        var building = await CreateBuildingAsync(
            EnAr("North Tower", "البرج الشمالي"),
            [Address("en", "  12 King Road, Amman  "), Address("ar", "١٢ شارع الملك، عمّان")]);

        Shown(await _buildings.GetAsync(building.Id))
            .ShouldBe(new[] { ("ar", "١٢ شارع الملك، عمّان"), ("en", "12 King Road, Amman") });
    }

    [Fact]
    public async Task An_address_is_optional()
    {
        var building = await CreateBuildingAsync(En("South Tower"), []);

        (await _buildings.GetAsync(building.Id)).Addresses.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_admin_changes_and_clears_addresses()
    {
        var building = await CreateBuildingAsync(
            EnAr("North Tower", "البرج الشمالي"),
            [Address("en", "12 King Road"), Address("ar", "١٢ شارع الملك")]);

        // English changed; Arabic left out, so it's cleared.
        var updated = await UpdateAsync(building, EnAr("North Tower", "البرج الشمالي"), [Address("en", "14 King Road")]);

        Shown(updated).ShouldBe(new[] { ("en", "14 King Road") });
        Shown(await _buildings.GetAsync(building.Id)).ShouldBe(new[] { ("en", "14 King Road") });
    }

    [Fact]
    public async Task Renaming_keeps_the_address()
    {
        var building = await CreateBuildingAsync(En("North Tower"), [Address("en", "12 King Road")]);

        var updated = await UpdateAsync(building, En("North Tower A"), building.Addresses);

        updated.Name.ShouldBe("North Tower A");
        Shown(updated).ShouldBe(new[] { ("en", "12 King Road") });
    }

    [Fact]
    public async Task An_address_needs_a_name_in_its_language()
    {
        // No Arabic name, so there's no Arabic row to hold the Arabic address: it's dropped.
        var building = await CreateBuildingAsync(En("North Tower"), [Address("en", "12 King Road"), Address("ar", "١٢ شارع الملك")]);

        Shown(await _buildings.GetAsync(building.Id)).ShouldBe(new[] { ("en", "12 King Road") });
    }

    [Fact]
    public async Task Removing_a_language_removes_its_address_with_its_name()
    {
        var building = await CreateBuildingAsync(
            EnAr("North Tower", "البرج الشمالي"),
            [Address("en", "12 King Road"), Address("ar", "١٢ شارع الملك")]);

        var updated = await UpdateAsync(building, En("North Tower"), building.Addresses);

        Shown(updated).ShouldBe(new[] { ("en", "12 King Road") });
    }
}
