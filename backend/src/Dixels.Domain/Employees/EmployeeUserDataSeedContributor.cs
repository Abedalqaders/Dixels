using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;

namespace Dixels.Employees;

/* Seeds a handful of employee accounts so the admin's Employees page (and each employee's
 * own sign-in) has real users to work with. There's no add/update/delete-employee feature —
 * accounts are provisioned by seed data only, same spirit as the "employee" role itself
 * (see RoleDataSeedContributor) being seeded rather than created through the UI. Building
 * assignment is deliberately NOT seeded here — that's the admin's own action through the
 * Employees page, not a fixture. */
public class EmployeeUserDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private const string EmployeeRoleName = "employee";

    // ABP's own default admin password ("1q2w3E*") — reused here rather than invented, so
    // every seeded account in this app follows the same known convention for local/dev use.
    private const string SeedPassword = "1q2w3E*";

    private static readonly (string UserName, string Name, string Surname, string Email)[] Employees =
    {
        ("jordan.reed", "Jordan", "Reed", "jordan.reed@dixels.io"),
        ("amira.hassan", "Amira", "Hassan", "amira.hassan@dixels.io"),
        ("leo.tran", "Leo", "Tran", "leo.tran@dixels.io"),
    };

    private readonly IdentityUserManager _userManager;
    private readonly IGuidGenerator _guidGenerator;

    public EmployeeUserDataSeedContributor(IdentityUserManager userManager, IGuidGenerator guidGenerator)
    {
        _userManager = userManager;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        foreach (var (userName, name, surname, email) in Employees)
        {
            if (await _userManager.FindByNameAsync(userName) is not null)
            {
                continue;
            }

            var user = new IdentityUser(_guidGenerator.Create(), userName, email, context?.TenantId)
            {
                Name = name,
                Surname = surname,
            };

            var createResult = await _userManager.CreateAsync(user, SeedPassword);
            if (!createResult.Succeeded)
            {
                throw new AbpException(
                    $"Could not create employee user '{userName}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }

            var roleResult = await _userManager.AddToRoleAsync(user, EmployeeRoleName);
            if (!roleResult.Succeeded)
            {
                throw new AbpException(
                    $"Could not add '{userName}' to the '{EmployeeRoleName}' role: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }
        }
    }
}
