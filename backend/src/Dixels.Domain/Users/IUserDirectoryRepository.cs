using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Identity;

namespace Dixels.Users;

/// <summary>
/// The Users-page queries ABP's own <see cref="IIdentityUserRepository"/> can't express:
/// filtering by the <see cref="DixelsUserConsts.BuildingIdPropertyName"/> column needs
/// <c>EF.Property</c>, which only the EF layer can reference, and ABP's list has no role filter.
/// </summary>
/// <remarks>
/// Deliberately not an <c>IRepository&lt;IdentityUser, Guid&gt;</c>: ABP would then expose
/// this class as that generic repository too, replacing the Identity module's own one.
/// </remarks>
public interface IUserDirectoryRepository
{
    /// <summary>Ordered by user name. <paramref name="filter"/> matches user name, name,
    /// surname or email (case-insensitive); <paramref name="buildingId"/> and
    /// <paramref name="roleId"/> null mean any.</summary>
    Task<List<IdentityUser>> GetListAsync(
        string? filter,
        Guid? buildingId,
        Guid? roleId,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);

    Task<long> GetCountAsync(string? filter, Guid? buildingId, Guid? roleId, CancellationToken cancellationToken = default);
}
