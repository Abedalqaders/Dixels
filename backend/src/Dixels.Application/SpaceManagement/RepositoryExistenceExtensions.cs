using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace Dixels.SpaceManagement;

/// <summary>"Is it there?" without loading it: for a parent that only has to exist.</summary>
public static class RepositoryExistenceExtensions
{
    /// <summary>
    /// The same not-found GetAsync gives (deleted ones count as missing), from one
    /// <c>EXISTS</c> instead of the row and its translations.
    /// </summary>
    public static async Task EnsureExistsAsync<TEntity>(this IReadOnlyRepository<TEntity, Guid> repository, Guid id)
        where TEntity : class, IEntity<Guid>
    {
        if (!await repository.AnyAsync(e => e.Id == id))
        {
            throw new EntityNotFoundException(typeof(TEntity), id);
        }
    }
}
