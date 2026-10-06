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
    public async Task With_nothing_held_anywhere_it_is_empty()
    {
        var preview = new ReservationImpactPreview(new IReservationImpactProvider[] { new FixedProvider(), new FixedProvider() });

        var impact = await preview.PersonLeavingAsync(Guid.NewGuid(), null!, "Moved");

        impact.Count.ShouldBe(0);
        impact.Items.ShouldBeEmpty();
    }
}
