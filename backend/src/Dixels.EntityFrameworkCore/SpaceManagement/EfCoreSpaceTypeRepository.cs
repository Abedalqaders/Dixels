using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dixels.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Volo.Abp;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dixels.SpaceManagement;

public class EfCoreSpaceTypeRepository : EfCoreRepository<DixelsDbContext, SpaceType, Guid>, ISpaceTypeRepository
{
    public EfCoreSpaceTypeRepository(IDbContextProvider<DixelsDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<bool> NameExistsAsync(string language, string name, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        var normalized = SpaceTypeTranslation.Normalize(name);
        // The default soft-delete filter already hides deleted types.
        return await (await GetQueryableAsync())
            .Where(t => t.Id != excludingId)
            .AnyAsync(t => t.Translations.Any(n => n.Language == language && n.NormalizedName == normalized),
                GetCancellationToken(cancellationToken));
    }

    public async Task<SpaceType?> FindByNameAsync(string language, string name, CancellationToken cancellationToken = default)
    {
        var normalized = SpaceTypeTranslation.Normalize(name);
        return await (await WithDetailsAsync())
            .FirstOrDefaultAsync(t => t.Translations.Any(n => n.Language == language && n.NormalizedName == normalized),
                GetCancellationToken(cancellationToken));
    }

    public async Task<SpaceType> InsertCheckedAsync(SpaceType spaceType, CancellationToken cancellationToken = default)
    {
        try
        {
            return await InsertAsync(spaceType, autoSave: true, GetCancellationToken(cancellationToken));
        }
        catch (DbUpdateException ex) when (IsNameTaken(ex))
        {
            throw NameTaken(ex);
        }
    }

    public async Task<SpaceType> UpdateCheckedAsync(SpaceType spaceType, CancellationToken cancellationToken = default)
    {
        try
        {
            return await UpdateAsync(spaceType, autoSave: true, GetCancellationToken(cancellationToken));
        }
        catch (DbUpdateException ex) when (IsNameTaken(ex))
        {
            throw NameTaken(ex);
        }
    }

    // Lost a race SpaceTypeManager's check couldn't see (another admin saved the same name a
    // moment earlier). The failed statement has rolled the transaction back, so nothing from
    // this request is committed. Postgres names the index; SQLite (tests) can't reach here,
    // since its connection is never shared by two requests at once.
    private static bool IsNameTaken(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName?.Contains("SpaceTypeTranslations", StringComparison.Ordinal) == true;

    private static BusinessException NameTaken(Exception inner) =>
        new(DixelsDomainErrorCodes.SpaceTypeNameAlreadyExistsConcurrently, innerException: inner);
}
