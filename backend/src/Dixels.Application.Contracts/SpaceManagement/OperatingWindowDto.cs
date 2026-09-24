namespace Dixels.SpaceManagement;

/// <summary>
/// Wire shape for an <c>OperatingWindow</c> value object — the domain type itself lives in
/// Dixels.Domain, which Application.Contracts can't reference, so DTOs use this primitive
/// shape instead. <see cref="Open"/>/<see cref="Close"/> are "HH:mm" strings and are
/// meaningless when <see cref="IsOpen24Hours"/> is true, matching the frontend's own
/// OperatingWindow port.
/// </summary>
public class OperatingWindowDto
{
    public bool IsOpen24Hours { get; set; }
    public string Open { get; set; } = string.Empty;
    public string Close { get; set; } = string.Empty;
}
