namespace Dixels.SpaceManagement;

/// <summary>A building, floor or space: made by an admin's "add", which can be retried (see <see cref="HierarchyCreateReplays"/>).</summary>
public interface IHasIdempotencyKey
{
    string? IdempotencyKey { get; }
}
