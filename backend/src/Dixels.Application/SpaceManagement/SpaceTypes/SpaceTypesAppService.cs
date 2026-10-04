using System;
using System.Collections.Generic;
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
    private readonly LocalizedNameReader _nameReader;

    public SpaceTypesAppService(
        ISpaceTypeRepository spaceTypeRepository,
        SpaceTypeManager spaceTypeManager,
        IMultiLingualObjectManager multiLingualObjectManager,
        LocalizedNameReader nameReader)
    {
        _spaceTypeRepository = spaceTypeRepository;
        _spaceTypeManager = spaceTypeManager;
        _multiLingualObjectManager = multiLingualObjectManager;
        _nameReader = nameReader;
    }

    public async Task<PagedResultDto<SpaceTypeDto>> GetListAsync(GetSpaceTypesInput input)
    {
        await CheckAnyPermissionAsync(DixelsPermissions.Readers.SpaceTypes);

        // Search matches a name in any language, ignoring case; the page is sorted by the name
        // the reader sees (theirs, else the default language's) — all in the database, like
        // the Buildings list.
        var (shown, fallback) = await _nameReader.GetLanguagesAsync();
        var queryable = await _spaceTypeRepository.WithDetailsAsync();

        if (!input.Filter.IsNullOrWhiteSpace())
        {
            var term = NameTranslation.Normalize(input.Filter!);
            queryable = queryable.Where(t => t.Translations.Any(n => n.NormalizedName.Contains(term)));
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var spaceTypes = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(t => t.Translations.Where(n => n.Language == shown).Select(n => n.Name).FirstOrDefault()
                    ?? t.Translations.Where(n => n.Language == fallback).Select(n => n.Name).FirstOrDefault())
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        // Each type's name in the reader's language (else the default language's), in one pass.
        var named = await _multiLingualObjectManager.GetBulkTranslationsAsync<SpaceType, SpaceTypeTranslation>(spaceTypes);

        return new PagedResultDto<SpaceTypeDto>(totalCount, named.Select(pair => ToDto(pair.entity, pair.translation)).ToList());
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
