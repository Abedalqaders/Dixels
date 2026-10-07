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
    private static AffectedReservationDto Item(string kind, int hour, string place) => Item(kind, new DateTime(2026, 10, 7, hour, 0, 0), place);

    private static AffectedReservationDto Item(string kind, DateTime start, string place) => new()
    {
        Kind = kind,
        Id = Guid.NewGuid(),
        PlaceName = place,
        LocalStart = start,
        LocalEnd = start.AddHours(1),
    };

    /// <summary>Answers every question with the same fixed items: all counted, the first ones returned, as a module does.</summary>
    private sealed class FixedProvider(params AffectedReservationDto[] items) : IReservationImpactProvider
    {
        private Task<ReservationImpactPart> First(int first) => Task.FromResult(new ReservationImpactPart(
            items.Length,
            items.Select(i => new RankedReservation(new DateTimeOffset(i.LocalStart, TimeSpan.Zero), i))
                .OrderBy(r => r.StartsAt)
                .Take(first)
                .ToList()));

        public Task<ReservationImpactPart> FindNoLongerFittingAsync(RoomRulesChange change, int first) => First(first);

        public Task<ReservationImpactPart> FindUpcomingAsync(Building building, RoomScope scope, string reason, int first) => First(first);

        public Task<List<AffectedReservation>> FindAffectedAsync(RoomRulesChange change) =>
            Task.FromResult(items.Select(i => new AffectedReservation(i.Kind, i.Id, i.Reasons.FirstOrDefault() ?? string.Empty)).ToList());

        public Task<int> CountUpcomingAsync(RoomScope scope) => Task.FromResult(items.Length);

        public Task<ReservationImpactPart> FindForPersonLeavingAsync(Guid userId, Building building, string reason, int first) => First(first);
    }

    [Fact]
    public async Task Merges_every_module_into_one_list_earliest_first()
    {
        var bookings = new FixedProvider(Item(ReservationKinds.Booking, 14, "Room 1"), Item(ReservationKinds.Booking, 9, "Desk 2"));
        var parking = new FixedProvider(Item("parking", 11, "Spot P4"));
        var preview = new ReservationImpactPreview(new IReservationImpactProvider[] { bookings, parking });

        var impact = await preview.UpcomingAsync(null!, new RoomScope(Guid.NewGuid()), "The building was removed");

        impact.Count.ShouldBe(3);
        impact.Items.Select(i => (i.Kind, i.PlaceName)).ShouldBe(new[]
        {
            (ReservationKinds.Booking, "Desk 2"),
            ("parking", "Spot P4"),
            (ReservationKinds.Booking, "Room 1"),
        });
    }

    [Fact]
    public async Task A_page_is_cut_from_every_module_merged_and_the_count_is_the_total()
    {
        // Bookings every hour from 00:00, parking every hour from 00:30: they interleave.
        var day = new DateTime(2026, 10, 7);
        var bookings = new FixedProvider(Enumerable.Range(0, 60).Select(i => Item(ReservationKinds.Booking, day.AddHours(i), "Room")).ToArray());
        var parking = new FixedProvider(Enumerable.Range(0, 60).Select(i => Item("parking", day.AddHours(i).AddMinutes(30), "Spot")).ToArray());
        var preview = new ReservationImpactPreview(new IReservationImpactProvider[] { bookings, parking });

        var page1 = await preview.UpcomingAsync(null!, new RoomScope(Guid.NewGuid()), "Removed", skip: 0);
        var page2 = await preview.UpcomingAsync(null!, new RoomScope(Guid.NewGuid()), "Removed", skip: ReservationImpactPreview.PageSize);
        var everything = await preview.UpcomingAsync(null!, new RoomScope(Guid.NewGuid()), "Removed");

        page1.Count.ShouldBe(120);
        page2.Count.ShouldBe(120);
        page1.Items.Count.ShouldBe(ReservationImpactPreview.PageSize);
        page2.Items.Count.ShouldBe(ReservationImpactPreview.PageSize);
        page1.Items.Concat(page2.Items).Select(i => i.Id)
            .ShouldBe(everything.Items.Take(2 * ReservationImpactPreview.PageSize).Select(i => i.Id));
        everything.Items.Count.ShouldBe(120);
        everything.Items.Select(i => i.LocalStart).ShouldBeInOrder();
    }

    [Fact]
    public async Task Counting_adds_up_every_module()
    {
        var bookings = new FixedProvider(Item(ReservationKinds.Booking, 14, "Room 1"), Item(ReservationKinds.Booking, 9, "Desk 2"));
        var parking = new FixedProvider(Item("parking", 11, "Spot P4"));
        var preview = new ReservationImpactPreview(new IReservationImpactProvider[] { bookings, parking });

        (await preview.CountUpcomingAsync(new RoomScope(Guid.NewGuid()))).ShouldBe(3);
    }

    [Fact]
    public async Task A_save_carries_on_every_module_s_reservations_and_the_first_reason_each()
    {
        var late = Item(ReservationKinds.Booking, 17, "Room 1");
        late.Reasons = new List<string> { "Open 09:00–17:00 only", "Up to 2h" };
        var spot = Item("parking", 11, "Spot P4");
        var preview = new ReservationImpactPreview(new IReservationImpactProvider[] { new FixedProvider(late), new FixedProvider(spot) });

        var affected = await preview.AffectedAsync(null!);

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
