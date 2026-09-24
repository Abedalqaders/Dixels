using Dixels.SpaceManagement.ValueObjects;

namespace Dixels.SpaceManagement;

/// <summary>
/// A Floor or Space's own days/hours override, reduced to just what
/// <see cref="ConstraintResolver.FindNarrowingConflicts"/> needs to check it against a
/// proposed, tighter parent value — kept independent of the <see cref="Floor"/>/<see cref="Space"/>
/// entities themselves so the check stays trivially unit-testable.
/// </summary>
public sealed record NarrowingCandidate(string DisplayName, OperatingDays? OwnDays, OperatingWindow? OwnHours);
