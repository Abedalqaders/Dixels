using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dixels.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace Dixels.Users;

public class EfCoreUserDirectoryRepository : IUserDirectoryRepository, ITransientDependency
{
    private readonly IDbContextProvider<DixelsDbContext> _dbContextProvider;

    public EfCoreUserDirectoryRepository(IDbContextProvider<DixelsDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
    }

    public async Task<List<IdentityUser>> GetListAsync(
        string? filter,
        Guid? buildingId,
        Guid? roleId,
        string? grantedPermission,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildQueryAsync(filter, buildingId, roleId, grantedPermission, cancellationToken);

        return await query
            .OrderBy(u => u.UserName)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetCountAsync(
        string? filter,
        Guid? buildingId,
        Guid? roleId,
        string? grantedPermission,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildQueryAsync(filter, buildingId, roleId, grantedPermission, cancellationToken);
        return await query.LongCountAsync(cancellationToken);
    }

    public async Task<List<IdentityUser>> GetActiveInBuildingAsync(
        Guid buildingId,
        IReadOnlyCollection<Guid> ids,
        IReadOnlyCollection<string> normalizedEmails,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0 && normalizedEmails.Count == 0)
        {
            return new List<IdentityUser>();
        }

        var dbContext = await _dbContextProvider.GetDbContextAsync();
        // Tracked, as in BuildQueryAsync: BuildingId reaches ExtraProperties only through tracking.
        return await dbContext.Users
            .Where(u => u.IsActive
                        && EF.Property<Guid?>(u, DixelsUserConsts.BuildingIdPropertyName) == buildingId
                        && (ids.Contains(u.Id) || normalizedEmails.Contains(u.NormalizedEmail)))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<IdentityUser>> SearchColleaguesAsync(
        Guid buildingId,
        string filter,
        Guid exceptUserId,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildQueryAsync(filter, buildingId, roleId: null, grantedPermission: null, cancellationToken);

        return await query
            .Where(u => u.IsActive && u.Id != exceptUserId)
            .OrderBy(u => u.Name)
            .ThenBy(u => u.Surname)
            .ThenBy(u => u.UserName)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);
    }

    private async Task<IQueryable<IdentityUser>> BuildQueryAsync(
        string? filter,
        Guid? buildingId,
        Guid? roleId,
        string? grantedPermission,
        CancellationToken cancellationToken)
    {
        var dbContext = await _dbContextProvider.GetDbContextAsync();
        // Tracked on purpose: ABP copies mapped extra-property columns (BuildingId) into
        // ExtraProperties from its ChangeTracker.Tracked hook — AsNoTracking would skip that
        // and every user would read back as unassigned.
        IQueryable<IdentityUser> query = dbContext.Users;

        if (buildingId is not null)
        {
            // The extra property is a mapped column (DixelsEfCoreEntityExtensionMappings),
            // so EF.Property turns this into a plain WHERE "BuildingId" = @id.
            query = query.Where(u => EF.Property<Guid?>(u, DixelsUserConsts.BuildingIdPropertyName) == buildingId);
        }

        if (roleId is not null)
        {
            // AbpUserRoles link rows: an EXISTS subquery, not a join, so no user appears twice.
            query = query.Where(u => u.Roles.Any(r => r.RoleId == roleId));
        }

        if (grantedPermission is not null)
        {
            // ABP keeps grants in AbpPermissionGrants keyed by provider: "R" rows name a role
            // (by its name), "U" rows a user (by id). The tenant filter applies as usual.
            var grants = dbContext.Set<PermissionGrant>().Where(g => g.Name == grantedPermission);

            var grantedRoleNames = grants
                .Where(g => g.ProviderName == RolePermissionValueProvider.ProviderName)
                .Select(g => g.ProviderKey);
            var grantedRoleIds = dbContext.Roles.Where(r => grantedRoleNames.Contains(r.Name)).Select(r => r.Id);

            // Direct grants are rare, so read them first: the key is a Guid's text, and comparing
            // it in SQL would depend on how each provider stores and formats a Guid column.
            var directUserIds = (await grants
                    .Where(g => g.ProviderName == UserPermissionValueProvider.ProviderName)
                    .Select(g => g.ProviderKey)
                    .ToListAsync(cancellationToken))
                .Select(key => Guid.TryParse(key, out var id) ? id : (Guid?)null)
                .Where(id => id is not null)
                .Select(id => id!.Value)
                .ToList();

            query = query.Where(u => u.Roles.Any(r => grantedRoleIds.Contains(r.RoleId)) || directUserIds.Contains(u.Id));
        }

        if (!filter.IsNullOrWhiteSpace())
        {
            // ToLower on both sides: case-insensitive on Postgres and on SQLite (tests) alike.
            var term = filter!.Trim().ToLower();
            query = query.Where(u =>
                u.UserName.ToLower().Contains(term) ||
                (u.Name != null && u.Name.ToLower().Contains(term)) ||
                (u.Surname != null && u.Surname.ToLower().Contains(term)) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        return query;
    }
}
