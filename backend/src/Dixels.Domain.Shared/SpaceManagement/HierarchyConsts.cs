namespace Dixels.SpaceManagement;

/// <summary>Shared by buildings, floors and spaces (Domain) and their create DTOs (Application.Contracts).</summary>
public static class HierarchyConsts
{
    /// <summary>The key an admin's "add" is sent with (the dialog sends a UUID, 36 characters).</summary>
    public const int MaxIdempotencyKeyLength = 64;
}
