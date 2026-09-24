using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Dixels.EntityFrameworkCore;

/// <summary>
/// EF Core mapping for the Building → Floor → Space hierarchy. Kept as its own extension
/// method (mirroring ABP's own <c>ConfigureIdentity()</c>-style calls in
/// <see cref="DixelsDbContext.OnModelCreating"/>) rather than inlined, so the hierarchy's
/// mapping can be read as one unit.
///
/// Everything here targets both Postgres (real deployment) and SQLite (this app's xUnit
/// test suite runs against SQLite in-memory) — see <see cref="OverrideScope"/>/
/// <see cref="OverrideEffect"/>/<see cref="ReasonCategory"/>'s <c>HasConversion&lt;string&gt;()</c>
/// below, which avoids Postgres-native enum/array types that SQLite has no equivalent for.
/// </summary>
public static class SpaceManagementModelBuilderExtensions
{
    // OperatingWindow is immutable (no setters, only a constructor), so unlike a plain
    // scalar property, EF Core won't auto-discover Open/Close/IsOpen24Hours by convention —
    // they must be declared explicitly so EF knows to bind them through the constructor.
    // Shared across Building/Floor/Space since it's identical everywhere Hours appears.
    private static void ConfigureHours<TOwner>(OwnedNavigationBuilder<TOwner, OperatingWindow> hours)
        where TOwner : class
    {
        hours.Property(h => h.IsOpen24Hours);
        hours.Property(h => h.Open);
        hours.Property(h => h.Close);
    }

    public static void ConfigureSpaceManagement(this ModelBuilder builder)
    {
        builder.Entity<SpaceType>(b =>
        {
            b.ToTable(DixelsConsts.DbTablePrefix + "SpaceTypes", DixelsConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).HasMaxLength(SpaceTypeConsts.MaxNameLength).IsRequired();
            b.Property(x => x.IconKey).HasConversion<string>().HasMaxLength(32).IsRequired();

            // A plain unique index would still block re-using "Desk" after an old "Desk" row
            // was soft-deleted, since the row still physically exists — the filter is what
            // makes a soft-deleted name reusable.
            b.HasIndex(x => x.Name).IsUnique().HasFilter("\"IsDeleted\" = false");
        });

        builder.Entity<Building>(b =>
        {
            b.ToTable(DixelsConsts.DbTablePrefix + "Buildings", DixelsConsts.DbSchema, tb =>
                tb.HasCheckConstraint("CK_AppBuildings_MaxHorizonDaysPositive", "\"MaxHorizonDays\" > 0"));
            b.ConfigureByConvention();

            b.Property(x => x.Name).HasMaxLength(BuildingConsts.MaxNameLength).IsRequired();
            b.Property(x => x.BuildingNumber).HasMaxLength(BuildingConsts.MaxBuildingNumberLength);
            b.Property(x => x.Timezone).HasMaxLength(BuildingConsts.MaxTimezoneLength).IsRequired();

            // Flattened to a single "Days" int column (the plan's "plain int bitmask column"),
            // rather than EF's default "Days_Mask" owned-column name.
            b.OwnsOne(x => x.Days, days => days.Property(d => d.Mask).HasColumnName("Days"));
            b.OwnsOne(x => x.Hours, ConfigureHours);
        });

        builder.Entity<Floor>(b =>
        {
            b.ToTable(DixelsConsts.DbTablePrefix + "Floors", DixelsConsts.DbSchema, tb =>
                tb.HasCheckConstraint("CK_AppFloors_HoursOpenCloseTogether", "(\"Hours_Open\" IS NULL) = (\"Hours_Close\" IS NULL)"));
            b.ConfigureByConvention();

            b.Property(x => x.Name).HasMaxLength(FloorConsts.MaxNameLength).IsRequired();

            b.OwnsOne(x => x.Days, days => days.Property(d => d.Mask).HasColumnName("Days"));
            b.OwnsOne(x => x.Hours, ConfigureHours);

            // Real FK + cascade is a schema-level backstop only — the app-level soft-delete
            // path (SpaceHierarchyManager, wired up in the application layer) is what
            // actually runs when an admin deletes a Building through the API.
            b.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId)
                .OnDelete(DeleteBehavior.Cascade).IsRequired();
        });

        builder.Entity<Space>(b =>
        {
            b.ToTable(DixelsConsts.DbTablePrefix + "Spaces", DixelsConsts.DbSchema, tb =>
            {
                tb.HasCheckConstraint("CK_AppSpaces_HoursOpenCloseTogether", "(\"Hours_Open\" IS NULL) = (\"Hours_Close\" IS NULL)");
                tb.HasCheckConstraint("CK_AppSpaces_CapacityPositive", "\"Capacity\" > 0");
                tb.HasCheckConstraint("CK_AppSpaces_MinAttendeesWithinCapacity", "\"MinAttendees\" IS NULL OR \"MinAttendees\" <= \"Capacity\"");
            });
            b.ConfigureByConvention();

            b.Property(x => x.Name).HasMaxLength(SpaceConsts.MaxNameLength).IsRequired();

            b.OwnsOne(x => x.Days, days => days.Property(d => d.Mask).HasColumnName("Days"));
            b.OwnsOne(x => x.Hours, ConfigureHours);

            b.HasOne<Floor>().WithMany().HasForeignKey(x => x.FloorId)
                .OnDelete(DeleteBehavior.Cascade).IsRequired();

            // Restrict, not Cascade: a SpaceType in use must block deletion (enforced in the
            // application layer), never silently take Spaces down with it.
            b.HasOne<SpaceType>().WithMany().HasForeignKey(x => x.SpaceTypeId)
                .OnDelete(DeleteBehavior.Restrict).IsRequired();
        });

        builder.Entity<AvailabilityOverride>(b =>
        {
            b.ToTable(DixelsConsts.DbTablePrefix + "AvailabilityOverrides", DixelsConsts.DbSchema, tb =>
                tb.HasCheckConstraint("CK_AppAvailabilityOverrides_EndsAfterStarts", "\"EndsAt\" > \"StartsAt\""));
            b.ConfigureByConvention();

            b.Property(x => x.Scope).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.Effect).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.ReasonCategory).HasConversion<string>().HasMaxLength(32).IsRequired();
            b.Property(x => x.ReasonDetail).HasMaxLength(AvailabilityOverrideConsts.MaxReasonDetailLength);

            // No real FK here — Scope+ScopeId points at whichever of the three tables Scope
            // names, which a single FK column can't express. This index is what keeps
            // "closures overlapping this Building/Floor/Space in this date range" fast.
            b.HasIndex(x => new { x.Scope, x.ScopeId, x.StartsAt, x.EndsAt });
        });
    }
}
