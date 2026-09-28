using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace Dixels.Identity;

/* Seeds the application roles. "admin" already exists — it's created
 * automatically by ABP's own IdentityDataSeedContributor, along with the
 * default admin user. This contributor only adds the roles specific to
 * this app. Runs automatically alongside every other IDataSeedContributor
 * whenever Dixels.DbMigrator seeds the database. */
public class RoleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string EmployeeRoleName = "employee";

    /* What an employee may do: see availability, book for themselves and cancel their own bookings. Spelled out as
     * strings because DixelsPermissions lives in Application.Contracts, which the Domain
     * layer can't reference — RoleDataSeedContributorTests pins these to the real
     * constants so a rename there can't silently leave employees without access. */
    public static readonly string[] EmployeePermissions =
    {
        "Dixels.Bookings",
        "Dixels.Bookings.Create",
        "Dixels.Bookings.Cancel",
    };

    private readonly IdentityRoleManager _roleManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IPermissionDataSeeder _permissionDataSeeder;

    public RoleDataSeedContributor(
        IdentityRoleManager roleManager,
        IGuidGenerator guidGenerator,
        IPermissionDataSeeder permissionDataSeeder)
    {
        _roleManager = roleManager;
        _guidGenerator = guidGenerator;
        _permissionDataSeeder = permissionDataSeeder;
    }

    // Set on the seed context once this has run. Other contributors call this one first to
    // be sure the role exists, so it can run several times in one seeding pass — and each
    // run only sees grants already saved, so without this a new permission was granted once
    // per call (three duplicate rows the first time Bookings.Cancel was seeded).
    private const string SeededPropertyName = "Dixels:RolesSeeded";

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?[SeededPropertyName] is true)
        {
            return;
        }

        await CreateRoleIfNotExistsAsync(EmployeeRoleName);

        // Idempotent across passes: grants only what the role doesn't already have saved.
        await _permissionDataSeeder.SeedAsync(
            RolePermissionValueProvider.ProviderName,
            EmployeeRoleName,
            EmployeePermissions,
            context?.TenantId);

        if (context is not null)
        {
            context[SeededPropertyName] = true;
        }
    }

    private async Task CreateRoleIfNotExistsAsync(string roleName)
    {
        if (await _roleManager.FindByNameAsync(roleName) != null)
        {
            return;
        }

        var role = new IdentityRole(_guidGenerator.Create(), roleName)
        {
            IsPublic = true
        };

        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            throw new AbpException(
                $"Could not create role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
}
