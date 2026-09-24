namespace Dixels.SpaceManagement;

/// <summary>
/// Shared between the <c>Building</c> entity's own validation (Domain) and its DTOs'
/// <c>[StringLength]</c> attributes (Application.Contracts) — one source of truth for both.
/// </summary>
public static class BuildingConsts
{
    public const int MaxNameLength = 128;
    public const int MaxBuildingNumberLength = 32;
    public const int MaxTimezoneLength = 64;
}
