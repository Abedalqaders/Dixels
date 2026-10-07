using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Volo.Abp.DependencyInjection;

namespace Dixels.Reservations;

/// <summary>
/// Asks every <see cref="IReservationImpactProvider"/> the same question and merges the
/// answers into one list, soonest first — what space management and user management show
/// an admin before a change. A new module is picked up by implementing the provider; nothing
/// here or in the callers changes.
///
/// The admin reads it <see cref="PageSize"/> at a time ("Show more"): pass <c>skip</c>, and
/// <see cref="ReservationImpactDto.Count"/> is still the full total. A save leaves
/// <c>skip</c> out and gets everything — it acts on all of them, not on what was shown.
/// </summary>
public class ReservationImpactPreview : ITransientDependency
{
    public const int PageSize = 50;

    private readonly IEnumerable<IReservationImpactProvider> _providers;

    public ReservationImpactPreview(IEnumerable<IReservationImpactProvider> providers)
    {
        _providers = providers;
    }

    /// <summary>Reservations the proposed rules (or a new closure) would no longer allow.</summary>
    public Task<ReservationImpactDto> NoLongerFittingAsync(RoomRulesChange change, int? skip = null) =>
        GatherAsync(skip, (provider, first) => provider.FindNoLongerFittingAsync(change, first));

    /// <summary>
    /// A preview as a save's event carries it on (<see cref="SpaceRulesChangedEvent.Affected"/>):
    /// which reservations, and the first reason each one breaks.
    /// </summary>
    public static IReadOnlyList<AffectedReservation> ToAffected(ReservationImpactDto impact) =>
        impact.Items.Select(i => new AffectedReservation(i.Kind, i.Id, i.Reasons.FirstOrDefault() ?? string.Empty)).ToList();

    /// <summary>Every upcoming reservation in <paramref name="scope"/>: what a delete would take.</summary>
    public Task<ReservationImpactDto> UpcomingAsync(Building building, RoomScope scope, string reason, int? skip = null) =>
        GatherAsync(skip, (provider, first) => provider.FindUpcomingAsync(building, scope, reason, first));

    /// <summary>How many upcoming reservations these rooms hold, across every module — when only the number matters.</summary>
    public async Task<int> CountUpcomingAsync(IReadOnlyCollection<Guid> spaceIds)
    {
        var count = 0;
        foreach (var provider in _providers)
        {
            count += await provider.CountUpcomingAsync(spaceIds);
        }

        return count;
    }

    /// <summary>What a person holds in a building they're leaving.</summary>
    public Task<ReservationImpactDto> PersonLeavingAsync(Guid userId, Building building, string reason, int? skip = null) =>
        GatherAsync(skip, (provider, first) => provider.FindForPersonLeavingAsync(userId, building, reason, first));

    // Each module answers with its total and its own first skip + PageSize; the page is cut
    // from those merged, so it's right however many modules answer. Soonest first, then by id,
    // so the order is the same on every call.
    private async Task<ReservationImpactDto> GatherAsync(int? skip, Func<IReservationImpactProvider, int, Task<ReservationImpactPart>> ask)
    {
        var from = skip is null ? 0 : Math.Clamp(skip.Value, 0, int.MaxValue - PageSize);
        var first = skip is null ? int.MaxValue : from + PageSize;

        var count = 0;
        var items = new List<RankedReservation>();
        foreach (var provider in _providers)
        {
            var part = await ask(provider, first);
            count += part.Count;
            items.AddRange(part.Items);
        }

        var ordered = items.OrderBy(i => i.StartsAt).ThenBy(i => i.Reservation.Id).Select(i => i.Reservation);
        return new ReservationImpactDto
        {
            Count = count,
            Items = (skip is null ? ordered : ordered.Skip(from).Take(PageSize)).ToList(),
        };
    }
}
