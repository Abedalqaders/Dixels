namespace Dixels.SpaceManagement;

/// <summary>
/// Shared between the <c>Space</c> entity's own validation (Domain) and its DTOs'
/// <c>[StringLength]</c> attributes (Application.Contracts) — one source of truth for both.
/// </summary>
public static class SpaceConsts
{
    public const int MaxNameLength = 128;
}
