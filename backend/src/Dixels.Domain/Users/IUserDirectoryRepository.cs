using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Identity;

namespace Dixels.Users;

/// <summary>
/// The Users-page queries ABP's own <see cref="IIdentityUserRepository"/> can't express:
/// filtering by the <see cref="DixelsUserConsts.BuildingIdPropertyName"/> column needs
/// <c>EF.Property</c>, which only the EF layer can reference, and ABP's list has no role or
/// permission filter.
/// </summary>
/// <remarks>
/// Deliberately not an <c>IRepository&lt;IdentityUser, Guid&gt;</c>: ABP would then expose
/// this class as that generic repository too, replacing the Identity module's own one.
/// </remarks>
public interface IUserDirectoryRepository
{
    /// <summary>Ordered by user name. <paramref name="filter"/> matches user name, name,
    /// surname or email (case-insensitive); <paramref name="grantedPermission"/> keeps the users
    /// holding that permission through a role or a direct grant. Null filters mean any.</summary>
    Task<List<IdentityUser>> GetListAsync(
        string? filter,
        Guid? buildingId,
        Guid? roleId,
        string? grantedPermission,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<long> GetCountAsync(
        string? filter,
        Guid? buildingId,
        Guid? roleId,
        string? grantedPermission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active users in <paramref name="buildingId"/> who have one of these ids or one of these
    /// emails (<paramref name="normalizedEmails"/> upper-cased, like IdentityUser.NormalizedEmail)
    /// — the people a booking there may invite as colleagues, in one query.
    /// </summary>
    Task<List<IdentityUser>> GetActiveInBuildingAsync(
        Guid buildingId,
        IReadOnlyCollection<Guid> ids,
        IReadOnlyCollection<string> normalizedEmails,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active users in <paramref name="buildingId"/> matching <paramref name="filter"/> (as in
    /// GetListAsync), except <paramref name="exceptUserId"/>, by name — the guest picker's search.
    /// </summary>
    Task<List<IdentityUser>> SearchColleaguesAsync(
        Guid buildingId,
        string filter,
        Guid exceptUserId,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="GetCountAsync"/> and <see cref="GetListAsync"/> together, for a page with its
    /// total: the filters (and a permission's direct grants) are worked out once for both.
    /// </summary>
    Task<(long TotalCount, List<IdentityUser> Users)> GetPageAsync(
        string? filter,
        Guid? buildingId,
        Guid? roleId,
        string? grantedPermission,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);
}
