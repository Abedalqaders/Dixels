namespace Dixels.SpaceManagement;

/// <summary>
/// Closures union rather than override: <see cref="Closed"/> at any level always wins on
/// overlap, while <see cref="Open"/> only extends bookability beyond the resolved operating
/// days/hours (e.g. a special Saturday opening) and is beaten by any overlapping
/// <see cref="Closed"/> override at any level.
/// </summary>
public enum OverrideEffect
{
    Closed,
    Open
}
