using System;
using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

/// <summary>
/// One atomic save covering every constraint field on the Building level — either the
/// whole page's changes land, or none do. <see cref="ConcurrencyStamp"/> must be the value
/// read at fetch time; a stale value throws a concurrency conflict rather than overwriting.
/// </summary>
public class UpdateBuildingConstraintsDto
{
    [Required]
    public int[] Days { get; set; } = Array.Empty<int>();

    [Required]
    public OperatingWindowDto Hours { get; set; } = new();

    public int MaxDurationMinutes { get; set; }
    public int MaxHorizonDays { get; set; }
    public int MinLeadMinutes { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
