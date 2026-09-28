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
using Volo.Abp.Identity;

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
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await BuildQueryAsync(filter, buildingId, roleId);

        return await query
            .OrderBy(u => u.UserName)
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetCountAsync(string? filter, Guid? buildingId, Guid? roleId, CancellationToken cancellationToken = default)
    {
        var query = await BuildQueryAsync(filter, buildingId, roleId);
        return await query.LongCountAsync(cancellationToken);
    }

    private async Task<IQueryable<IdentityUser>> BuildQueryAsync(string? filter, Guid? buildingId, Guid? roleId)
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
