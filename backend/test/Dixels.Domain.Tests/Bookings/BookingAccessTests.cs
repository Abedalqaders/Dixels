using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Dixels.Bookings;

/// <summary>
/// Who may do what with one booking: the organiser, a colleague guest, or nobody — and what
/// each refusal looks like (404 for nobody, so a guessed id reveals nothing; 403 for a guest).
/// </summary>
public class BookingAccessTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid GuestId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    private readonly Booking _booking = new(
        Guid.NewGuid(), Guid.NewGuid(), OwnerId, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
        attendees: 2, "Planning", "{}", Guid.NewGuid().ToString());

    private readonly IBookingRepository _bookings = Substitute.For<IBookingRepository>();
    private readonly BookingAccess _access;

    public BookingAccessTests()
    {
        _bookings.IsInviteeAsync(_booking.Id, GuestId, Arg.Any<CancellationToken>()).Returns(true);
        _access = new BookingAccess(_bookings);
    }

    [Fact]
    public async Task The_organiser_is_the_owner_without_asking_the_database()
    {
        (await _access.GetRoleAsync(_booking, OwnerId)).ShouldBe(BookingRole.Owner);
        await _bookings.DidNotReceiveWithAnyArgs().IsInviteeAsync(default, default);
    }

    [Fact]
    public async Task A_colleague_with_a_guest_row_is_a_guest_and_anyone_else_is_no_one()
    {
        (await _access.GetRoleAsync(_booking, GuestId)).ShouldBe(BookingRole.Guest);
        (await _access.GetRoleAsync(_booking, StrangerId)).ShouldBe(BookingRole.None);
    }

    [Fact]
    public async Task An_allowed_role_passes()
    {
        await _access.EnsureAsync(_booking, OwnerId, BookingRole.Owner);
        await _access.EnsureAsync(_booking, GuestId, BookingRole.Owner, BookingRole.Guest);
    }

    [Fact]
    public async Task To_someone_with_no_role_the_booking_does_not_exist()
    {
        var ex = await Should.ThrowAsync<EntityNotFoundException>(() => _access.EnsureAsync(_booking, StrangerId, BookingRole.Owner, BookingRole.Guest));
        ex.Id.ShouldBe(_booking.Id);
    }

    [Fact]
    public async Task A_guest_asking_for_an_organiser_only_action_is_told_so()
    {
        (await Should.ThrowAsync<BusinessException>(() => _access.EnsureAsync(_booking, GuestId, BookingRole.Owner)))
            .Code.ShouldBe(DixelsDomainErrorCodes.BookingOrganiserOnly);
        (await Should.ThrowAsync<BusinessException>(() =>
                _access.EnsureAsync(_booking, GuestId, DixelsDomainErrorCodes.BookingOnlyOrganiserCancels, BookingRole.Owner)))
            .Code.ShouldBe(DixelsDomainErrorCodes.BookingOnlyOrganiserCancels);
    }
}
