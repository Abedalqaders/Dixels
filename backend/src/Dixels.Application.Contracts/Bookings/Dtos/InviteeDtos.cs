using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Timing;

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

    /// <summary>
    /// Previews only: a colleague who has their own booking or an accepted meeting at that
    /// time. A heads-up, never a refusal; it never says with what.
    /// </summary>
    public bool IsBusy { get; set; }

    /// <summary>Previews only: on how many of the dates they're busy (a series: "busy on 2 of 8 dates"; one booking: 0 or 1).</summary>
    public int BusyDates { get; set; }

    /// <summary>Previews only: when they're busy, cut to the booking's time on each date — times only, never with what.</summary>
    public List<BusyTimeDto> BusyTimes { get; set; } = new();
}

/// <summary>A stretch of someone's busy time, on the building's wall clock (not normalized to UTC).</summary>
public class BusyTimeDto
{
    [DisableDateTimeNormalization]
    public DateTime LocalStart { get; set; }

    [DisableDateTimeNormalization]
    public DateTime LocalEnd { get; set; }
}

/// <summary>The colleagues an Edit guests dialog lists, to learn which are busy at the booking's time.</summary>
public class BusyGuestsInput
{
    [MaxLength(BookingConsts.MaxInvitees)]
    public List<Guid> UserIds { get; set; } = new();
}

/// <summary>A colleague who is busy then, and on how many of the dates (always 1 for one booking).</summary>
public class BusyGuestDto
{
    public Guid UserId { get; set; }
    public int BusyDates { get; set; }

    /// <summary>When, cut to the booking's time on each date (see <see cref="BookingInviteeDto.BusyTimes"/>).</summary>
    public List<BusyTimeDto> Times { get; set; } = new();
}

/// <summary>The busy colleagues among those asked about, and how many dates were checked ("busy on 2 of 8 dates").</summary>
public class BusyGuestsResultDto
{
    /// <summary>1 for one booking; for a series, its upcoming dates.</summary>
    public int Dates { get; set; }

    public List<BusyGuestDto> Items { get; set; } = new();
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
}
