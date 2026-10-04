using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Localization;
using Dixels.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.MultiLingualObjects;

namespace Dixels.SpaceManagement;

// Only signed-in users at class level: reads admit several permissions (see
// DixelsPermissions.Readers), and ABP adds a class-level [Authorize(...)] to every method's own.
// So every method states what it needs — a new one must too.
[Authorize]
public class SpaceTypesAppService : DixelsAppService, ISpaceTypesAppService
{
    private readonly ISpaceTypeRepository _spaceTypeRepository;
    private readonly SpaceTypeManager _spaceTypeManager;
    private readonly IMultiLingualObjectManager _multiLingualObjectManager;

    public SpaceTypesAppService(
        ISpaceTypeRepository spaceTypeRepository,
        SpaceTypeManager spaceTypeManager,
        IMultiLingualObjectManager multiLingualObjectManager)
    {
        _spaceTypeRepository = spaceTypeRepository;
        _spaceTypeManager = spaceTypeManager;
        _multiLingualObjectManager = multiLingualObjectManager;
    }

    public async Task<ListResultDto<SpaceTypeDto>> GetListAsync()
    {
        await CheckAnyPermissionAsync(DixelsPermissions.Readers.SpaceTypes);
        var spaceTypes = await _spaceTypeRepository.GetListAsync(includeDetails: true);

        // Each type's name in the reader's language (else the default language's), in one pass.
        var named = await _multiLingualObjectManager.GetBulkTranslationsAsync<SpaceType, SpaceTypeTranslation>(spaceTypes);
        var byShownName = StringComparer.Create(CultureInfo.CurrentUICulture, ignoreCase: true);

        return new ListResultDto<SpaceTypeDto>(
            named.Select(pair => ToDto(pair.entity, pair.translation)).OrderBy(dto => dto.Name, byShownName).ToList());
    }

    [Authorize(DixelsPermissions.SpaceTypes.Create)]
    public async Task<SpaceTypeDto> CreateAsync(CreateSpaceTypeDto input)
    {
        var spaceType = await _spaceTypeManager.CreateAsync(input.Names.ToNames(), input.IconKey);
        await _spaceTypeRepository.InsertCheckedAsync(spaceType);

        return await ToDtoAsync(spaceType);
    }

    [Authorize(DixelsPermissions.SpaceTypes.Edit)]
    public async Task<SpaceTypeDto> UpdateAsync(Guid id, UpdateSpaceTypeDto input)
    {
        var spaceType = await _spaceTypeRepository.GetAsync(id);

        await _spaceTypeManager.SetNamesAsync(spaceType, input.Names.ToNames());
        spaceType.SetIconKey(input.IconKey);

        await _spaceTypeRepository.UpdateCheckedAsync(spaceType);

        return await ToDtoAsync(spaceType);
    }

    [Authorize(DixelsPermissions.SpaceTypes.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var spaceType = await _spaceTypeRepository.GetAsync(id);
        await _spaceTypeManager.PrepareForDeletionAsync(spaceType);
        await _spaceTypeRepository.DeleteAsync(spaceType);
    }

    private async Task<SpaceTypeDto> ToDtoAsync(SpaceType spaceType)
    {
        var translation = await _multiLingualObjectManager.GetTranslationAsync<SpaceType, SpaceTypeTranslation>(spaceType);
        return ToDto(spaceType, translation);
    }

    private SpaceTypeDto ToDto(SpaceType spaceType, SpaceTypeTranslation? shown)
    {
        var dto = ObjectMapper.Map<SpaceType, SpaceTypeDto>(spaceType);
        // Never null in practice: the default language's name is required.
        dto.Name = shown?.Name ?? string.Empty;
        dto.Names = spaceType.Translations.ToNameDtos();
        return dto;
    }
}
