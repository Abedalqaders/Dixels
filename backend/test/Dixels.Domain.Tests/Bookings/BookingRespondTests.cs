using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Guids;
using Xunit;

namespace Dixels.Bookings;

/// <summary>A guest's answer to one date: when it's taken, and when answers are closed.</summary>
public class BookingRespondTests
{
    private static readonly Guid GuestId = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private static Booking NewBooking()
    {
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Start, Start.AddHours(1),
            attendees: 2, "Planning", "{}", Guid.NewGuid().ToString());
        booking.SetInvitees(new[] { new Invitee(GuestId, null, null) }, SimpleGuidGenerator.Instance);
        return booking;
    }

    [Fact]
    public void An_answer_is_kept_with_when_it_was_given_and_can_be_changed_until_the_start()
    {
        var booking = NewBooking();
        var row = booking.Invitees.Single();
        row.ResponseStatus.ShouldBe(InviteeResponseStatus.Pending);

        booking.Respond(GuestId, InviteeResponseStatus.Accepted, Start.AddDays(-2));
        booking.Respond(GuestId, InviteeResponseStatus.Declined, Start.AddMinutes(-1));

        row.ResponseStatus.ShouldBe(InviteeResponseStatus.Declined);
        row.RespondedAt.ShouldBe(Start.AddMinutes(-1));
    }

    [Fact]
    public void Answers_close_when_the_meeting_starts()
    {
        var booking = NewBooking();

        Should.Throw<BusinessException>(() => booking.Respond(GuestId, InviteeResponseStatus.Accepted, Start))
            .Code.ShouldBe(DixelsDomainErrorCodes.BookingResponseClosed);
    }

    [Fact]
    public void Answers_close_when_the_booking_is_cancelled()
    {
        var booking = NewBooking();
        booking.Cancel(Guid.NewGuid(), Start.AddDays(-1), reason: null, byAdmin: true);

        Should.Throw<BusinessException>(() => booking.Respond(GuestId, InviteeResponseStatus.Declined, Start.AddDays(-1)))
            .Code.ShouldBe(DixelsDomainErrorCodes.BookingResponseClosed);
    }

    [Fact]
    public void Nobody_answers_pending()
    {
        var booking = NewBooking();

        Should.Throw<ArgumentOutOfRangeException>(() => booking.Respond(GuestId, InviteeResponseStatus.Pending, Start.AddDays(-1)));
    }
}
