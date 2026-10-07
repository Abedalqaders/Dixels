using Dixels.Bookings;
using Dixels.SpaceManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

            // "My bookings" between two times (calendar, own-clash check) asks for
            // EndsAt > start AND StartsAt < end. EndsAt is what skips the person's history;
            // bookings are short, so StartsAt < end only trims a few rows. On (UserId, StartsAt)
            // every past booking was read, more each year.
            b.HasIndex(x => new { x.UserId, x.EndsAt });

            // The reminder job's lookup, every minute: only rows still waiting for one.
            b.HasIndex(x => x.StartsAt)
                .HasDatabaseName("IX_AppBookings_ReminderDue")
                .HasFilter("\"ReminderSentAt\" IS NULL AND \"Status\" = 'Confirmed'");

            // Restrict, not cascade: booking history outlives the space (spaces are only
            // ever soft-deleted, so this never blocks a normal delete).
            b.HasOne<Space>().WithMany().HasForeignKey(x => x.SpaceId)
                .OnDelete(DeleteBehavior.Restrict).IsRequired();

            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict).IsRequired();

            // A series' bookings are found by it (cancel "this and following", the repeat
            // icon). Restrict: a series row is never deleted, like the bookings themselves.
            b.HasOne<BookingSeries>().WithMany().HasForeignKey(x => x.SeriesId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.SeriesId);

            // Its guests, through the read-only Invitees list's backing field.
            b.HasMany(x => x.Invitees).WithOne().HasForeignKey(x => x.BookingId)
                .OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation(x => x.Invitees).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<BookingAttendee>(b =>
        {
            b.ToTable(DixelsConsts.DbTablePrefix + "BookingAttendees", DixelsConsts.DbSchema, tb =>
                tb.HasCheckConstraint("CK_AppBookingAttendees_UserOrEmail", "(\"UserId\" IS NULL) <> (\"Email\" IS NULL)"));
            b.ConfigureByConvention();
            ConfigureInviteeRow(b);

            // Loading a booking's guests. (The unique index below only holds colleagues.)
            b.HasIndex(x => x.BookingId);

            // A colleague is on a booking once (the domain checks this first; this is the backstop).
            b.HasIndex(x => new { x.BookingId, x.UserId }).IsUnique().HasFilter("\"UserId\" IS NOT NULL");

            // "Bookings I'm invited to between two times" — seeks to the ones ending after the
            // window starts, like (UserId, EndsAt) on the bookings themselves, instead of reading
            // every invitation the person ever had. Only colleagues have a UserId.
            b.HasIndex(x => new { x.UserId, x.EndsAt })
                .HasFilter("\"UserId\" IS NOT NULL")
                .IncludeProperties(x => x.BookingId);
        });

        builder.Entity<BookingSeries>(b =>
        {
            b.ToTable(DixelsConsts.DbTablePrefix + "BookingSeries", DixelsConsts.DbSchema, tb =>
            {
                tb.HasCheckConstraint("CK_AppBookingSeries_IntervalPositive", "\"Interval\" > 0");
                tb.HasCheckConstraint("CK_AppBookingSeries_DurationPositive", "\"DurationMinutes\" > 0");
            });
            b.ConfigureByConvention();

            b.Property(x => x.Title).HasMaxLength(BookingConsts.MaxTitleLength).IsRequired();
            b.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.MonthlyRepeat).HasConversion<string>().HasMaxLength(16).IsRequired();
            b.Property(x => x.IdempotencyKey).HasMaxLength(BookingConsts.MaxSeriesIdempotencyKeyLength).IsRequired();

            b.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();

            b.HasOne<Space>().WithMany().HasForeignKey(x => x.SpaceId)
                .OnDelete(DeleteBehavior.Restrict).IsRequired();
            b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict).IsRequired();

            b.HasMany(x => x.Invitees).WithOne().HasForeignKey(x => x.SeriesId)
                .OnDelete(DeleteBehavior.Cascade).IsRequired();
            b.Navigation(x => x.Invitees).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<BookingSeriesAttendee>(b =>
        {
            b.ToTable(DixelsConsts.DbTablePrefix + "BookingSeriesAttendees", DixelsConsts.DbSchema, tb =>
                tb.HasCheckConstraint("CK_AppBookingSeriesAttendees_UserOrEmail", "(\"UserId\" IS NULL) <> (\"Email\" IS NULL)"));
            b.ConfigureByConvention();
            ConfigureInviteeRow(b);

            b.HasIndex(x => x.SeriesId);
            b.HasIndex(x => new { x.SeriesId, x.UserId }).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
        });
    }

    /// <summary>
    /// What a booking's and a series' guest rows share. Restrict to the user: users are only
    /// soft-deleted, and a colleague who leaves is taken off upcoming bookings by the app.
    /// </summary>
    private static void ConfigureInviteeRow<T>(EntityTypeBuilder<T> b)
        where T : InviteeRow
    {
        b.Property(x => x.Email).HasMaxLength(BookingConsts.MaxInviteeEmailLength);
        b.Property(x => x.Name).HasMaxLength(BookingConsts.MaxInviteeNameLength);
        b.Property(x => x.ResponseStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.IcsUid).HasMaxLength(BookingConsts.MaxIcsUidLength).IsRequired();

        // A reply to an invite is matched to its guest by this alone (E6).
        b.HasIndex(x => x.IcsUid).IsUnique();

        b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
