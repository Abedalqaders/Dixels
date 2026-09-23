using Microsoft.EntityFrameworkCore;
using Volo.Abp;

namespace Dixels.SpaceOs.EntityFrameworkCore;

public static class SpaceOsDbContextModelCreatingExtensions
{
    public static void ConfigureSpaceOs(
        this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        /* Configure all entities here. Example:

        builder.Entity<Question>(b =>
        {
            //Configure table & schema name
            b.ToTable(SpaceOsDbProperties.DbTablePrefix + "Questions", SpaceOsDbProperties.DbSchema);

            b.ConfigureByConvention();

            //Properties
            b.Property(q => q.Title).IsRequired().HasMaxLength(QuestionConsts.MaxTitleLength);

            //Relations
            b.HasMany(question => question.Tags).WithOne().HasForeignKey(qt => qt.QuestionId);

            //Indexes
            b.HasIndex(q => q.CreationTime);
        });
        */
    }
}
