using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Settings;
using Dixels.Users;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;
using Volo.Abp.Settings;

namespace Dixels.Bookings;

/// <summary>
/// Turns the guest list a booker typed into the one that's stored, or says what's wrong with
/// it. Preview and create both run it, so the form shows exactly what will be saved.
///
/// A colleague is an active user of the space's building. A typed email that belongs to one
/// becomes that colleague (so they see the booking in their calendar), and anyone else's
/// email stays an external guest. The owner counts toward the head count but isn't a guest.
/// </summary>
public class BookingInviteeResolver : DomainService
{
    private readonly IUserDirectoryRepository _userDirectory;
    private readonly ISettingProvider _settingProvider;

    public BookingInviteeResolver(IUserDirectoryRepository userDirectory, ISettingProvider settingProvider)
    {
        _userDirectory = userDirectory;
        _settingProvider = settingProvider;
    }

    /// <summary>
    /// The checked list, in the order asked: colleagues with their current name and email (for
    /// showing), external guests with the email and name trimmed. One query, and none at all
    /// for an empty list.
    /// </summary>
    public async Task<IReadOnlyList<Invitee>> ResolveAsync(Guid ownerId, Guid buildingId, IReadOnlyCollection<Invitee> requested)
    {
        if (requested.Count == 0)
        {
            return Array.Empty<Invitee>();
        }

        foreach (var invitee in requested)
        {
            var hasUser = invitee.UserId is { } id && id != Guid.Empty;
            var hasEmail = !string.IsNullOrWhiteSpace(invitee.Email);
            if (hasUser == hasEmail)
            {
                throw new BusinessException(DixelsDomainErrorCodes.InviteeInvalid);
            }
        }

        var ids = requested.Where(i => i.UserId is not null).Select(i => i.UserId!.Value).Distinct().ToList();
        var emails = requested.Where(i => i.UserId is null).Select(i => Invitee.NormalizeEmail(i.Email!)).Distinct().ToList();
        var colleagues = await _userDirectory.GetActiveInBuildingAsync(buildingId, ids, emails);

        var byId = colleagues.ToDictionary(u => u.Id);
        var byEmail = colleagues
            .Where(u => u.NormalizedEmail is not null)
            .GroupBy(u => u.NormalizedEmail!)
            .ToDictionary(g => g.Key, g => g.First());

        var resolved = requested.Select(invitee =>
        {
            if (invitee.UserId is { } userId)
            {
                // Someone who doesn't exist reads the same as someone elsewhere, so ids can't be probed.
                return byId.TryGetValue(userId, out var user)
                    ? Colleague(user)
                    : throw new BusinessException(DixelsDomainErrorCodes.InviteeNotInBuilding);
            }

            var email = invitee.Email!.Trim();
            return byEmail.TryGetValue(Invitee.NormalizeEmail(email), out var match)
                ? Colleague(match)
                : new Invitee(null, email, string.IsNullOrWhiteSpace(invitee.Name) ? null : invitee.Name.Trim());
        }).ToList();

        // Checked after matching, so a typed colleague's email still works with outsiders off.
        if (resolved.Any(i => i.IsExternal) && !await _settingProvider.IsTrueAsync(DixelsSettings.ExternalGuestsEnabled))
        {
            throw new BusinessException(DixelsDomainErrorCodes.ExternalGuestsDisabled);
        }

        if (resolved.Any(i => i.UserId == ownerId))
        {
            throw new BusinessException(DixelsDomainErrorCodes.InviteeIsOwner);
        }

        var twice = resolved.GroupBy(i => i.Key).FirstOrDefault(g => g.Count() > 1);
        if (twice is not null)
        {
            var who = twice.First();
            throw new BusinessException(DixelsDomainErrorCodes.InviteeDuplicate)
                .WithData("invitee", who.Name ?? who.Email!);
        }

        return resolved;
    }

    private static Invitee Colleague(IdentityUser user) => new(user.Id, user.Email, user.DisplayName());
}
