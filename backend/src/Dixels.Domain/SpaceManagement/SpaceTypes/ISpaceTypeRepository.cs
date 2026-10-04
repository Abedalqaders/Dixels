using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dixels.SpaceManagement;

public interface ISpaceTypeRepository : IRepository<SpaceType, Guid>
{
    /// <summary>
    /// Whether a live (not deleted) space type other than <paramref name="excludingId"/> is
    /// already called <paramref name="name"/> in <paramref name="language"/> — compared the
    /// same way as the unique index: trimmed, ignoring case.
    /// </summary>
    Task<bool> NameExistsAsync(string language, string name, Guid? excludingId = null, CancellationToken cancellationToken = default);

    /// <summary>The live space type called <paramref name="name"/> in <paramref name="language"/>, with its names.</summary>
    Task<SpaceType?> FindByNameAsync(string language, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts and saves at once. Two admins saving the same name at the same moment both
    /// pass SpaceTypeManager's check; the database's unique index stops the second, and this
    /// reports it as SpaceTypeNameAlreadyExistsConcurrently instead of a server error.
    /// </summary>
    Task<SpaceType> InsertCheckedAsync(SpaceType spaceType, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="InsertCheckedAsync"/>
    Task<SpaceType> UpdateCheckedAsync(SpaceType spaceType, CancellationToken cancellationToken = default);
}
