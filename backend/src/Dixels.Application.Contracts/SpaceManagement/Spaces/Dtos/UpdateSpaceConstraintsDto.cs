using System.ComponentModel.DataAnnotations;

namespace Dixels.SpaceManagement;

/// <summary>
/// One atomic save covering every constraint field on the Space level. Each field null =
/// clear to inherit, non-null = override. <see cref="ConcurrencyStamp"/> must be the value
/// read at fetch time.
/// </summary>
public class UpdateSpaceConstraintsDto
{
    public int[]? Days { get; set; }
    public OperatingWindowDto? Hours { get; set; }
    public int? MaxDurationMinutes { get; set; }
    public int? MinAttendees { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}
