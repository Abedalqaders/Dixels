using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

/// <summary>
/// One atomic save covering every constraint field on the Floor level — either the whole
/// page's changes land, or none do. Each field null = clear to inherit, non-null = override.
/// <see cref="ConcurrencyStamp"/> must be the value read at fetch time.
/// </summary>
public class UpdateFloorConstraintsDto
{
    public int[]? Days { get; set; }
    public OperatingWindowDto? Hours { get; set; }
    public int? MaxDurationMinutes { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
