using System;
using System.Collections.Generic;
using System.Linq;
using Dixels.SpaceManagement;
using Dixels.SpaceManagement.ValueObjects;
using Shouldly;
using Xunit;

namespace Dixels.Bookings.Tests;

public class BookingPolicyValidatorTests
{
    private const int SlotMinutes = 15;

    private static readonly OperatingDays SunToThu = OperatingDays.FromDayOfWeeks(new[]
    {
        DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
    });

    private readonly BookingPolicyValidator _validator = new();

    // Asia/Amman is UTC+3 all year (no DST since 2022), which keeps the arithmetic in these
    // tests readable. DST behaviour is covered separately in BuildingClockTests.
    private readonly BuildingClock _clock = new("Asia/Amman");

    // Monday 28 Sep 2026, 09:00 in Amman.
    private DateTimeOffset Now => Local(2026, 9, 28, 9, 0);

    private DateTimeOffset Local(int year, int month, int day, int hour, int minute = 0)
        => _clock.ToUtc(new DateTime(year, month, day, hour, minute, 0));

    /// <summary>
    /// A Sun–Thu 07:00–20:00 floor (days + hours come from the Floor), a space capping
    /// duration at 2h with 8 seats and a minimum of 4, a 14-day horizon and 15 minutes' notice.
    /// </summary>
    private static ResolvedConstraints Rules(
        OperatingDays? days = null,
        OperatingWindow? hours = null,
        int maxDurationMinutes = 120,
        int? minAttendees = 4,
        int capacity = 8)
        => new(
            "Asia/Amman",
            new FieldValue<OperatingDays>(days ?? SunToThu, ConstraintSource.Floor),
            new FieldValue<OperatingWindow>(hours ?? OperatingWindow.Create(new TimeOnly(7, 0), new TimeOnly(20, 0)), ConstraintSource.Floor),
            new FieldValue<int>(maxDurationMinutes, ConstraintSource.Space),
            MaxHorizonDays: 14,
            MinLeadMinutes: 15,
            minAttendees,
            capacity);

    private IReadOnlyList<BookingViolation> Validate(
        DateTimeOffset start,
        DateTimeOffset end,
        int attendees = 4,
        ResolvedConstraints? rules = null,
        IReadOnlyList<OverrideWindow>? overrides = null,
        bool overlaps = false,
        int invitees = 0)
        => _validator.Validate(
            rules ?? Rules(),
            _clock,
            new BookingRequest(start, end, attendees, invitees),
            overrides ?? Array.Empty<OverrideWindow>(),
            overlaps,
            Now,
            SlotMinutes);

    private static OverrideWindow Override(DateTimeOffset start, DateTimeOffset end, OverrideEffect effect, OverrideScope scope)
        => new(new TimeRange(start, end), effect, scope, ReasonCategory.Maintenance, "HVAC work");

    private static string[] Codes(IEnumerable<BookingViolation> violations) => violations.Select(v => v.Code).ToArray();

    [Fact]
    public void A_booking_inside_every_rule_passes()
    {
        Validate(Local(2026, 9, 29, 10), Local(2026, 9, 29, 12)).ShouldBeEmpty();
    }

    [Fact]
    public void Too_many_attendees_is_rejected_at_space_level()
    {
        var violation = Validate(Local(2026, 9, 29, 10), Local(2026, 9, 29, 11), attendees: 9).ShouldHaveSingleItem();

        violation.Code.ShouldBe(DixelsDomainErrorCodes.BookingOverCapacity);
        violation.Level.ShouldBe(ConstraintSource.Space);
        violation.Data["capacity"].ShouldBe(8);
        violation.Data["attendees"].ShouldBe(9);
    }

    [Fact]
    public void Too_few_attendees_is_rejected_when_the_space_sets_a_minimum()
    {
        var violation = Validate(Local(2026, 9, 29, 10), Local(2026, 9, 29, 11), attendees: 3).ShouldHaveSingleItem();

        violation.Code.ShouldBe(DixelsDomainErrorCodes.BookingBelowMinAttendees);
        violation.Data["minAttendees"].ShouldBe(4);
    }

    [Fact]
    public void A_room_minimum_says_how_many_more_people_to_invite()
    {
        // The owner and one guest, in a room for at least 4: two more to invite.
        var violation = Validate(Local(2026, 9, 29, 10), Local(2026, 9, 29, 11), attendees: 2, invitees: 1).ShouldHaveSingleItem();

        violation.Code.ShouldBe(DixelsDomainErrorCodes.BookingBelowMinAttendees);
        violation.Data["minAttendees"].ShouldBe(4);
        violation.Data["toInvite"].ShouldBe(2);
    }

    [Fact]
    public void A_head_count_of_exactly_the_invitees_plus_the_owner_passes()
    {
        Validate(Local(2026, 9, 29, 10), Local(2026, 9, 29, 11), attendees: 5, invitees: 4).ShouldBeEmpty();
    }

    [Fact]
    public void A_guest_edit_checks_capacity_only_when_the_head_count_goes_up()
    {
        // The room seats 8; a booking kept at 10 when it shrank may stay at 10 or go down.
        _validator.ValidateHeadCount(Rules(), storedAttendees: 10, attendees: 10, invitees: 2).ShouldBeEmpty();
        _validator.ValidateHeadCount(Rules(), storedAttendees: 10, attendees: 9, invitees: 2).ShouldBeEmpty();
        Codes(_validator.ValidateHeadCount(Rules(), storedAttendees: 9, attendees: 10, invitees: 2))
            .ShouldBe(new[] { DixelsDomainErrorCodes.BookingOverCapacity });
    }

    [Fact]
    public void A_guest_edit_checks_the_minimum_only_when_the_head_count_goes_down()
    {
        // The room needs 4; a booking kept at 3 when the minimum rose may stay at 3 or go up.
        _validator.ValidateHeadCount(Rules(), storedAttendees: 3, attendees: 3, invitees: 2).ShouldBeEmpty();
        Codes(_validator.ValidateHeadCount(Rules(), storedAttendees: 3, attendees: 2, invitees: 1))
            .ShouldBe(new[] { DixelsDomainErrorCodes.BookingBelowMinAttendees });
    }

    [Fact]
    public void No_minimum_means_one_attendee_is_fine()
    {
        Validate(Local(2026, 9, 29, 10), Local(2026, 9, 29, 11), attendees: 1, rules: Rules(minAttendees: null)).ShouldBeEmpty();
    }

    [Fact]
    public void Longer_than_the_max_duration_is_rejected_naming_the_level_that_set_it()
    {
        var violation = Validate(Local(2026, 9, 29, 10), Local(2026, 9, 29, 12, 30)).ShouldHaveSingleItem();

        violation.Code.ShouldBe(DixelsDomainErrorCodes.BookingTooLong);
        violation.Level.ShouldBe(ConstraintSource.Space);
        violation.Data["maxDuration"].ShouldBe(TimeSpan.FromHours(2));
        violation.Data["requested"].ShouldBe(TimeSpan.FromMinutes(150));
    }

    [Fact]
    public void A_day_the_floor_never_opens_is_a_days_rejection()
    {
        // Friday 2 Oct.
        var violation = Validate(Local(2026, 10, 2, 10), Local(2026, 10, 2, 11)).ShouldHaveSingleItem();

        violation.Code.ShouldBe(DixelsDomainErrorCodes.BookingClosedDay);
        violation.Level.ShouldBe(ConstraintSource.Floor);
        violation.Data["day"].ShouldBe(DayOfWeek.Friday);
        ((OperatingDays)violation.Data["openDays"]).ToDayOfWeeks().ShouldBe(new[]
        {
            DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        });
    }

    [Fact]
    public void Running_past_closing_time_is_an_hours_rejection()
    {
        var violation = Validate(Local(2026, 9, 29, 19), Local(2026, 9, 29, 20, 30)).ShouldHaveSingleItem();

        violation.Code.ShouldBe(DixelsDomainErrorCodes.BookingOutsideHours);
        var hours = (OperatingWindow)violation.Data["hours"];
        hours.Open.ShouldBe(new TimeOnly(7, 0));
        hours.Close.ShouldBe(new TimeOnly(20, 0));
    }

    [Fact]
    public void Ending_exactly_at_closing_time_is_allowed()
    {
        Validate(Local(2026, 9, 29, 18), Local(2026, 9, 29, 20)).ShouldBeEmpty();
    }

    [Fact]
    public void A_24_7_space_accepts_a_booking_across_midnight()
    {
        var rules = Rules(days: OperatingDays.Everyday, hours: OperatingWindow.FullDay);

        Validate(Local(2026, 9, 29, 23), Local(2026, 9, 30, 1), rules: rules).ShouldBeEmpty();
    }

    [Fact]
    public void A_wrapping_window_accepts_a_booking_inside_it_and_rejects_one_past_its_close()
    {
        var rules = Rules(days: OperatingDays.Everyday, hours: OperatingWindow.Create(new TimeOnly(22, 0), new TimeOnly(2, 0)));

        Validate(Local(2026, 9, 29, 23), Local(2026, 9, 30, 1), rules: rules).ShouldBeEmpty();
        Codes(Validate(Local(2026, 9, 30, 1), Local(2026, 9, 30, 3), rules: rules))
            .ShouldBe(new[] { DixelsDomainErrorCodes.BookingOutsideHours });
    }

    [Fact]
    public void A_special_opening_makes_a_closed_day_bookable()
    {
        var friday = Override(Local(2026, 10, 2, 9), Local(2026, 10, 2, 17), OverrideEffect.Open, OverrideScope.Building);

        Validate(Local(2026, 10, 2, 10), Local(2026, 10, 2, 11), overrides: new[] { friday }).ShouldBeEmpty();
    }

    [Fact]
    public void A_closure_wins_over_a_special_opening_at_any_level()
    {
        var open = Override(Local(2026, 10, 2, 9), Local(2026, 10, 2, 17), OverrideEffect.Open, OverrideScope.Space);
        var closed = Override(Local(2026, 10, 2, 10), Local(2026, 10, 2, 12), OverrideEffect.Closed, OverrideScope.Building);

        var violation = Validate(Local(2026, 10, 2, 10), Local(2026, 10, 2, 11), overrides: new[] { open, closed }).ShouldHaveSingleItem();

        violation.Code.ShouldBe(DixelsDomainErrorCodes.BookingSpaceClosed);
        violation.Level.ShouldBe(ConstraintSource.Building);
        violation.Data["reason"].ShouldBe("HVAC work");
    }

    [Fact]
    public void A_closure_that_ends_exactly_when_the_booking_starts_does_not_block_it()
    {
        var closed = Override(Local(2026, 9, 29, 8), Local(2026, 9, 29, 10), OverrideEffect.Closed, OverrideScope.Floor);

        Validate(Local(2026, 9, 29, 10), Local(2026, 9, 29, 11), overrides: new[] { closed }).ShouldBeEmpty();
    }

    [Fact]
    public void Beyond_the_horizon_is_rejected_but_the_last_day_is_bookable_until_its_end()
    {
        // Today is 28 Sep and the horizon is 14 days, so 12 Oct is the last bookable date.
        Validate(Local(2026, 10, 12, 18), Local(2026, 10, 12, 20)).ShouldBeEmpty();

        var violation = Validate(Local(2026, 10, 13, 10), Local(2026, 10, 13, 11)).ShouldHaveSingleItem();
        violation.Code.ShouldBe(DixelsDomainErrorCodes.BookingBeyondHorizon);
        violation.Level.ShouldBe(ConstraintSource.Building);
        violation.Data["lastDate"].ShouldBe(new DateOnly(2026, 10, 12));
    }

    [Fact]
    public void A_start_in_the_past_gets_its_own_message()
    {
        Codes(Validate(Local(2026, 9, 28, 8), Local(2026, 9, 28, 10)))
            .ShouldBe(new[] { DixelsDomainErrorCodes.BookingStartInPast });
    }

    [Fact]
    public void Less_notice_than_the_lead_time_is_rejected()
    {
        // Now is 09:00, the lead time is 15 minutes: 09:15 is the earliest start.
        Codes(Validate(Local(2026, 9, 28, 9, 0), Local(2026, 9, 28, 10)))
            .ShouldBe(new[] { DixelsDomainErrorCodes.BookingTooSoon });
        Validate(Local(2026, 9, 28, 9, 15), Local(2026, 9, 28, 10)).ShouldBeEmpty();
    }

    [Fact]
    public void Times_off_the_slot_grid_are_rejected()
    {
        Codes(Validate(Local(2026, 9, 29, 10, 10), Local(2026, 9, 29, 11)))
            .ShouldBe(new[] { DixelsDomainErrorCodes.BookingNotAligned });
    }

    [Fact]
    public void An_overlap_comes_after_the_room_not_fitting()
    {
        Codes(Validate(Local(2026, 9, 29, 10), Local(2026, 9, 29, 11), attendees: 9, overlaps: true))
            .ShouldBe(new[] { DixelsDomainErrorCodes.BookingOverCapacity, DixelsDomainErrorCodes.BookingOverlap });
    }

    [Fact]
    public void Every_violation_is_returned_biggest_blocker_first()
    {
        var closed = Override(Local(2026, 10, 2, 0), Local(2026, 10, 3, 0), OverrideEffect.Closed, OverrideScope.Space);

        // Friday 2 Oct under a space closure, with too many people, for too long, and taken:
        // "closed" leads (shortening wouldn't help), "too long" comes last.
        var codes = Codes(Validate(Local(2026, 10, 2, 9), Local(2026, 10, 2, 12), attendees: 10, overrides: new[] { closed }, overlaps: true));

        codes.ShouldBe(new[]
        {
            DixelsDomainErrorCodes.BookingSpaceClosed,
            DixelsDomainErrorCodes.BookingClosedDay,
            DixelsDomainErrorCodes.BookingOverCapacity,
            DixelsDomainErrorCodes.BookingOverlap,
            DixelsDomainErrorCodes.BookingTooLong,
        });
    }

    [Fact]
    public void A_date_that_cant_be_booked_comes_before_the_room_being_closed()
    {
        var codes = Codes(Validate(Local(2026, 9, 25, 9), Local(2026, 9, 25, 12)));

        codes[0].ShouldBe(DixelsDomainErrorCodes.BookingStartInPast);
        codes[^1].ShouldBe(DixelsDomainErrorCodes.BookingTooLong);
    }
}
