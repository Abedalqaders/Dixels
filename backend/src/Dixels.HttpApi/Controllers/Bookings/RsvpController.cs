using System.Threading.Tasks;
using Dixels.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Controllers.Bookings;

/// <summary>
/// The public answer page's calls (no sign-in: the guest's link is the proof). POSTs only, so
/// the token never sits in a URL a proxy logs, and nothing is changed by a plain GET.
/// </summary>
[RemoteService(Name = DixelsRemoteServiceConsts.RemoteServiceName)]
[Area(DixelsRemoteServiceConsts.ModuleName)]
[Route("api/app/rsvp")]
[AllowAnonymous]
public class RsvpController : DixelsController, IRsvpAppService
{
    private readonly IRsvpAppService _rsvpAppService;

    public RsvpController(IRsvpAppService rsvpAppService)
    {
        _rsvpAppService = rsvpAppService;
    }

    [HttpPost("lookup")]
    public virtual Task<GuestInvitationDto> LookupAsync([FromBody] GuestLinkInput input) => _rsvpAppService.LookupAsync(input);

    [HttpPost]
    public virtual Task<GuestInvitationDto> AnswerAsync([FromBody] GuestAnswerInput input) => _rsvpAppService.AnswerAsync(input);
}
