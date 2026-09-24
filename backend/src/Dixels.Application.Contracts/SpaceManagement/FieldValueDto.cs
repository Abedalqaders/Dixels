namespace Dixels.SpaceManagement;

/// <summary>
/// A resolved constraint value paired with which level actually supplied it — "Building",
/// "Floor", or "Space" — so the frontend's "People will see" summary can label each value
/// "Custom for this room" / "Same as {level}" without guessing.
/// </summary>
public class FieldValueDto<T>
{
    public T Value { get; set; } = default!;
    public string Source { get; set; } = string.Empty;
}
