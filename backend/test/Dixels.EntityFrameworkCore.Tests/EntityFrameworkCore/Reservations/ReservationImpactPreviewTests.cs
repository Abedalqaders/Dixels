using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Reservations;
using Dixels.SpaceManagement;
using Shouldly;
using Xunit;

namespace Dixels.EntityFrameworkCore.Reservations;

/// <summary>
/// The preview asks every module that holds reservations and shows one list. A new module
/// (parking, say) is picked up by implementing the provider — nothing else changes.
/// </summary>
public class ReservationImpactPreviewTests
{
    private static AffectedReservationDto Item(string kind, int hour, string place) => new()
    {
        Kind = kind,
        Id = Guid.NewGuid(),
        PlaceName = place,
        LocalStart = new DateTime(2026, 10, 7, hour, 0, 0),
        LocalEnd = new DateTime(2026, 10, 7, hour + 1, 0, 0),
    };

    /// <summary>Answers every question with the same fixed items.</summary>
    private sealed class FixedProvider(params AffectedReservationDto[] items) : IReservationImpactProvider
    {
        public Task<List<AffectedReservationDto>> FindNoLongerFittingAsync(RoomRulesChange change) => Task.FromResult(items.ToList());

        public Task<List<AffectedReservationDto>> FindUpcomingAsync(Building building, IReadOnlyList<(Space Space, Floor Floor)> rooms, string reason) =>
            Task.FromResult(items.ToList());

        public Task<int> CountUpcomingAsync(IReadOnlyCollection<Guid> spaceIds) => Task.FromResult(items.Length);

        public Task<List<AffectedReservationDto>> FindForPersonLeavingAsync(Guid userId, Building building, string reason) =>
            Task.FromResult(items.ToList());
    }

    [Fact]
    public async Task Merges_every_module_into_one_list_earliest_first()
    {
        var bookings = new FixedProvider(Item(ReservationKinds.Booking, 14, "Room 1"), Item(ReservationKinds.Booking, 9, "Desk 2"));
        var parking = new FixedProvider(Item("parking", 11, "Spot P4"));
        var preview = new ReservationImpactPreview(new IReservationImpactProvider[] { bookings, parking });

        var impact = await preview.UpcomingAsync(null!, Array.Empty<(Space, Floor)>(), "The building was removed");

        impact.Count.ShouldBe(3);
        impact.Items.Select(i => (i.Kind, i.PlaceName)).ShouldBe(new[]
        {
            (ReservationKinds.Booking, "Desk 2"),
            ("parking", "Spot P4"),
            (ReservationKinds.Booking, "Room 1"),
        });
    }

    [Fact]
    public async Task Counting_adds_up_every_module()
    {
        var bookings = new FixedProvider(Item(ReservationKinds.Booking, 14, "Room 1"), Item(ReservationKinds.Booking, 9, "Desk 2"));
        var parking = new FixedProvider(Item("parking", 11, "Spot P4"));
        var preview = new ReservationImpactPreview(new IReservationImpactProvider[] { bookings, parking });

        (await preview.CountUpcomingAsync(new[] { Guid.NewGuid() })).ShouldBe(3);
    }

    [Fact]
    public void A_preview_is_carried_on_as_which_reservations_and_the_first_reason_each()
    {
        var late = Item(ReservationKinds.Booking, 17, "Room 1");
        late.Reasons = new List<string> { "Open 09:00–17:00 only", "Up to 2h" };
        var spot = Item("parking", 11, "Spot P4");

        var affected = ReservationImpactPreview.ToAffected(new ReservationImpactDto { Count = 2, Items = new List<AffectedReservationDto> { late, spot } });

        affected.ShouldBe(new[]
        {
            new AffectedReservation(ReservationKinds.Booking, late.Id, "Open 09:00–17:00 only"),
            new AffectedReservation("parking", spot.Id, string.Empty),
        });
    }

    [Fact]
    public async Task With_nothing_held_anywhere_it_is_empty()
    {
        var preview = new ReservationImpactPreview(new IReservationImpactProvider[] { new FixedProvider(), new FixedProvider() });

        var impact = await preview.PersonLeavingAsync(Guid.NewGuid(), null!, "Moved");

        impact.Count.ShouldBe(0);
        impact.Items.ShouldBeEmpty();
    }
}
