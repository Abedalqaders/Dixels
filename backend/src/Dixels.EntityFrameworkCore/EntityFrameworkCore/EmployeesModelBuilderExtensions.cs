using Microsoft.EntityFrameworkCore;
using Dixels.Employees;
using Dixels.SpaceManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Identity;

namespace Dixels.EntityFrameworkCore;

/// <summary>
/// EF Core mapping for the Employees vertical — one table, mirroring how
/// <see cref="SpaceManagementModelBuilderExtensions"/> keeps its own mapping as its own
/// extension method rather than inlined into <see cref="DixelsDbContext.OnModelCreating"/>.
/// </summary>
public static class EmployeesModelBuilderExtensions
{
    public static void ConfigureEmployees(this ModelBuilder builder)
    {
        builder.Entity<EmployeeBuildingAssignment>(b =>
        {
            b.ToTable(DixelsConsts.DbTablePrefix + "EmployeeBuildingAssignments", DixelsConsts.DbSchema);
            b.ConfigureByConvention();

            // One assignment per employee — SetBuilding overwrites in place rather than a
            // second row ever being inserted, but this is the real backstop against it.
            b.HasIndex(x => x.EmployeeUserId).IsUnique();

            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.EmployeeUserId)
                .OnDelete(DeleteBehavior.Cascade).IsRequired();

            b.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId)
                .OnDelete(DeleteBehavior.Cascade).IsRequired();
        });
    }
}
