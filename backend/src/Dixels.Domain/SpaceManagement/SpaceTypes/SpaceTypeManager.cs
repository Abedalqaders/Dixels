using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.SpaceManagement;

/// <summary>
/// Owns the two SpaceType invariants that need repository access (and so can't live on the
/// entity itself): name uniqueness among non-deleted rows, and refusing to delete a type
/// that's still assigned to a Space.
/// </summary>
public class SpaceTypeManager : DomainService
{
    private readonly IRepository<SpaceType, Guid> _spaceTypeRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;

    public SpaceTypeManager(IRepository<SpaceType, Guid> spaceTypeRepository, IRepository<Space, Guid> spaceRepository)
    {
        _spaceTypeRepository = spaceTypeRepository;
        _spaceRepository = spaceRepository;
    }

    public async Task<SpaceType> CreateAsync(string name, IconKey iconKey)
    {
        await EnsureNameIsUniqueAsync(name);
        return new SpaceType(GuidGenerator.Create(), name, iconKey);
    }

    public async Task RenameAsync(SpaceType spaceType, string name)
    {
        if (spaceType.Name != name)
        {
            await EnsureNameIsUniqueAsync(name, spaceType.Id);
        }

        spaceType.SetName(name);
    }

    public async Task EnsureNotInUseAsync(Guid spaceTypeId)
    {
        if (await _spaceRepository.AnyAsync(s => s.SpaceTypeId == spaceTypeId))
        {
            throw new BusinessException(DixelsDomainErrorCodes.SpaceTypeInUse);
        }
    }

    private async Task EnsureNameIsUniqueAsync(string name, Guid? excludingId = null)
    {
        // The repository's default query filter already excludes soft-deleted rows, matching
        // the partial unique index's WHERE "IsDeleted" = false — a soft-deleted name is free.
        var existing = await _spaceTypeRepository.FirstOrDefaultAsync(t => t.Name == name);
        if (existing is not null && existing.Id != excludingId)
        {
            throw new BusinessException(DixelsDomainErrorCodes.SpaceTypeNameAlreadyExists)
                .WithData("name", name);
        }
    }
}
