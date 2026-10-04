using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dixels.Localization;

namespace Dixels.SpaceManagement;

/// <summary>Identity/physical fields — Days/Hours/MaxDurationMinutes/MinAttendees go
/// through <see cref="UpdateSpaceConstraintsDto"/> instead, since those need the atomic,
/// concurrency-checked save path.</summary>
public class UpdateSpaceDto
{
    /// <summary>Every name it should have, one per language — a language left out loses its name. The default language's is required.</summary>
    [Required]
    [MinLength(1)]
    public List<LocalizedNameDto> Names { get; set; } = [];

    [Required]
    public Guid SpaceTypeId { get; set; }

    [Required]
    public int Capacity { get; set; }

    /// <summary>Also cancel the upcoming bookings for more people than the new capacity (default: keep them).</summary>
    public bool CancelAffectedBookings { get; set; }
}
