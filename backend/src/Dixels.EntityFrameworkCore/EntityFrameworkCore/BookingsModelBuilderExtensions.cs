using Dixels.Bookings;
using Dixels.SpaceManagement;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Identity;

namespace Dixels.EntityFrameworkCore;

/// <summary>
/// EF Core mapping for the Bookings vertical, kept as its own extension method like
/// <see cref="SpaceManagementModelBuilderExtensions"/>.
///
/// The no-overlap guarantee itself is NOT declared here: a Postgres exclusion constraint
/// has no EF Core model equivalent (and no SQLite one for the test suite), so it's created
/// with raw SQL in the Add_Bookings migration. Everything declared here works on both.
/// </summary>
public static class BookingsModelBuilderExtensions
{
    public static void ConfigureBookings(this ModelBuilder builder)
    {
        builder.Entity<Booking>(b =>
        {
            b.ToTable(DixelsConsts.DbTablePrefix + "Bookings", DixelsConsts.DbSchema, tb =>
            {
                tb.HasCheckConstraint("CK_AppBookings_EndsAfterStarts", "\"EndsAt\" > \"StartsAt\"");
                tb.HasCheckConstraint("CK_AppBookings_AttendeesPositive", "\"Attendees\" > 0");
            });
            b.ConfigureByConvention();

            b.Property(x => x.Title).HasMaxLength(BookingConsts.MaxTitleLength).IsRequired();
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.IdempotencyKey).HasMaxLength(BookingConsts.MaxIdempotencyKeyLength).IsRequired();
            b.Property(x => x.CancelReason).HasMaxLength(BookingConsts.MaxCancelReasonLength);

            // jsonb on Postgres keeps the snapshot queryable later (e.g. "which bookings were
            // accepted under a 20:00 close"); SQLite accepts the type name as plain text.
            b.Property(x => x.ResolvedConstraintsJson).HasColumnType("jsonb").IsRequired();

            // A retry with the same key must find the first attempt's booking — and two
            // attempts can never both insert.
            b.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();

            // The overlap check and the availability timeline both look up by space + time.
            b.HasIndex(x => new { x.SpaceId, x.StartsAt, x.EndsAt });

            // "My bookings" lists by user + time.
            b.HasIndex(x => new { x.UserId, x.StartsAt });

            // Restrict, not cascade: booking history outlives the space (spaces are only
            // ever soft-deleted, so this never blocks a normal delete).
            b.HasOne<Space>().WithMany().HasForeignKey(x => x.SpaceId)
                .OnDelete(DeleteBehavior.Restrict).IsRequired();

            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict).IsRequired();
        });
    }
}
