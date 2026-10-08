using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dixels.Bookings;

/// <summary>
/// One person to invite: a colleague by <see cref="UserId"/> (picked from the colleague
/// search), or an external guest by <see cref="Email"/> with an optional <see cref="Name"/>.
/// Set exactly one of UserId / Email. A typed email that belongs to a colleague in the
/// space's building is stored as that colleague.
/// </summary>
public class InviteeDto
{
    public Guid? UserId { get; set; }

    [EmailAddress]
    [StringLength(BookingConsts.MaxInviteeEmailLength)]
    public string? Email { get; set; }

    /// <summary>External guests only; ignored for a colleague.</summary>
    [StringLength(BookingConsts.MaxInviteeNameLength)]
    public string? Name { get; set; }
}

/// <summary>An invitee as stored (and as a preview would store them).</summary>
public class BookingInviteeDto
{
    /// <summary>Set for a colleague; null for an external guest.</summary>
    public Guid? UserId { get; set; }

    /// <summary>A colleague's current name; an external's name as typed, or empty.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Only the booking's owner sees the guests' emails; anyone else gets it empty.</summary>
    public string Email { get; set; } = string.Empty;

    public bool IsExternal { get; set; }

    /// <summary>Their answer: Pending until they accept or decline. Everyone invited sees everyone's.</summary>
    public InviteeResponseStatus ResponseStatus { get; set; }
}

/// <summary>A colleague guest's answer to an invitation: Accepted or Declined.</summary>
public class RespondToInviteDto : IValidatableObject
{
    public InviteeResponseStatus Status { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status is not (InviteeResponseStatus.Accepted or InviteeResponseStatus.Declined))
        {
            yield return new ValidationResult("An answer is Accepted or Declined.", new[] { nameof(Status) });
        }
    }
}

/// <summary>The owner changing who's invited after booking — and the head count, which must leave room for them.</summary>
public class UpdateInviteesDto
{
    [MaxLength(BookingConsts.MaxInvitees)]
    public List<InviteeDto> Invitees { get; set; } = new();

    [Range(1, int.MaxValue)]
    public int Attendees { get; set; } = 1;
}
