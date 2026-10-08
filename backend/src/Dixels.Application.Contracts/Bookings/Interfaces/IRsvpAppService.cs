using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dixels.Bookings;

/// <summary>
/// The public answer page behind a guest's link (no sign-in: the link is the proof). Both are
/// POSTs, so the token never sits in a URL a proxy logs. An unknown or forged link, or a guest no
/// longer invited, is 404 <c>GuestLinkNotFound</c>.
/// </summary>
public interface IRsvpAppService : IApplicationService
{
    /// <summary>The invitation and the guest's current answer; changes nothing (opening the page).</summary>
    Task<GuestInvitationDto> LookupAsync(GuestLinkInput input);

    /// <summary>
    /// Saves their answer (a series link: every upcoming date) and returns the invitation as it
    /// now stands. 400 <c>ResponseClosed</c> once it has started or was cancelled.
    /// </summary>
    Task<GuestInvitationDto> AnswerAsync(GuestAnswerInput input);
}
