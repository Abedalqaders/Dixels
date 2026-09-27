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

    /* What an employee may do: see availability and book for themselves. Spelled out as
     * strings because DixelsPermissions lives in Application.Contracts, which the Domain
     * layer can't reference — RoleDataSeedContributorTests pins these to the real
     * constants so a rename there can't silently leave employees without access. */
    public static readonly string[] EmployeePermissions =
    {
        "Dixels.Bookings",
        "Dixels.Bookings.Create",
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

    public async Task SeedAsync(DataSeedContext context)
    {
        await CreateRoleIfNotExistsAsync(EmployeeRoleName);

        // Idempotent: grants only what the role doesn't already have.
        await _permissionDataSeeder.SeedAsync(
            RolePermissionValueProvider.ProviderName,
            EmployeeRoleName,
            EmployeePermissions,
            context?.TenantId);
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
