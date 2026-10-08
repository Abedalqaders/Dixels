using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dixels.Bookings;

/// <summary>A guest's secret link token (from the answer buttons in their email).</summary>
public class GuestLinkInput
{
    [Required]
    [StringLength(128)]
    public string Token { get; set; } = string.Empty;
}

/// <summary>Their answer, given on the public answer page.</summary>
public class GuestAnswerInput : GuestLinkInput, IValidatableObject
{
    /// <summary>Accepted or Declined (Pending isn't an answer), as in <see cref="RespondToInviteDto"/>.</summary>
    public InviteeResponseStatus Answer { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Answer is not (InviteeResponseStatus.Accepted or InviteeResponseStatus.Declined))
        {
            yield return new ValidationResult("An answer is Accepted or Declined.", new[] { nameof(Answer) });
        }
    }
}

/// <summary>
/// What the public answer page shows a guest: only what their invite email already told them
/// (never the other guests). Names are in the request's language; times on the building's clock.
/// </summary>
public class GuestInvitationDto
{
    /// <summary>The guest's own name, for the greeting (an outsider's email when they gave none).</summary>
    public string GuestName { get; set; } = string.Empty;

    /// <summary>The booker.</summary>
    public string InvitedBy { get; set; } = string.Empty;

    public string? Title { get; set; }
    public string SpaceName { get; set; } = string.Empty;
    public string FloorName { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string? Address { get; set; }

    /// <summary>The meeting, or a series' next date, building-local.</summary>
    public DateTime LocalStart { get; set; }
    public DateTime LocalEnd { get; set; }

    /// <summary>A series: its rule (the answer covers every upcoming date). Null for one booking.</summary>
    public RecurrenceDto? Recurrence { get; set; }

    public InviteeResponseStatus MyResponse { get; set; }

    /// <summary>False once it has started (a series: its last date) or was cancelled: the page shows "closed".</summary>
    public bool IsOpen { get; set; }

    /// <summary>The booker's language: the page opens in it, as the invite was written in it.</summary>
    public string Language { get; set; } = string.Empty;
}
