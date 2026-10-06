using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.SpaceManagement;
using Volo.Abp.DependencyInjection;

namespace Dixels.Reservations;

/// <summary>
/// Asks every <see cref="IReservationImpactProvider"/> the same question and merges the
/// answers into one list, earliest first — what space management and user management show
/// an admin before a change. A new module is picked up by implementing the provider; nothing
/// here or in the callers changes.
/// </summary>
public class ReservationImpactPreview : ITransientDependency
{
    private readonly IEnumerable<IReservationImpactProvider> _providers;

    public ReservationImpactPreview(IEnumerable<IReservationImpactProvider> providers)
    {
        _providers = providers;
    }

    /// <summary>Reservations the proposed rules (or a new closure) would no longer allow.</summary>
    public Task<ReservationImpactDto> NoLongerFittingAsync(RoomRulesChange change) =>
        GatherAsync(provider => provider.FindNoLongerFittingAsync(change));

    /// <summary>
    /// A preview as a save's event carries it on (<see cref="SpaceRulesChangedEvent.Affected"/>):
    /// which reservations, and the first reason each one breaks.
    /// </summary>
    public static IReadOnlyList<AffectedReservation> ToAffected(ReservationImpactDto impact) =>
        impact.Items.Select(i => new AffectedReservation(i.Kind, i.Id, i.Reasons.FirstOrDefault() ?? string.Empty)).ToList();

    /// <summary>Every upcoming reservation in these rooms: what a delete would take.</summary>
    public Task<ReservationImpactDto> UpcomingAsync(Building building, IReadOnlyList<(Space Space, Floor Floor)> rooms, string reason) =>
        GatherAsync(provider => provider.FindUpcomingAsync(building, rooms, reason));

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
    public Task<ReservationImpactDto> PersonLeavingAsync(Guid userId, Building building, string reason) =>
        GatherAsync(provider => provider.FindForPersonLeavingAsync(userId, building, reason));

    private async Task<ReservationImpactDto> GatherAsync(Func<IReservationImpactProvider, Task<List<AffectedReservationDto>>> ask)
    {
        var items = new List<AffectedReservationDto>();
        foreach (var provider in _providers)
        {
            items.AddRange(await ask(provider));
        }

        items = items.OrderBy(i => i.LocalStart).ThenBy(i => i.PlaceName).ToList();
        return new ReservationImpactDto { Count = items.Count, Items = items };
    }
}
