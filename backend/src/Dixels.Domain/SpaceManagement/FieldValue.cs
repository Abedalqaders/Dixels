namespace Dixels.SpaceManagement;

/// <summary>
/// A resolved constraint value together with which level of the hierarchy supplied it.
/// Provenance isn't decoration — it's what lets a future rejection message name the exact
/// level that blocked a request without duplicating the hierarchy logic in the error path.
/// </summary>
public sealed record FieldValue<T>(T Value, ConstraintSource Source);
