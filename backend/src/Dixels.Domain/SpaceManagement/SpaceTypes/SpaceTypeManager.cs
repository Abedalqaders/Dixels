using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Localization;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.SpaceManagement;

/// <summary>
/// Owns the SpaceType invariants that need repository access (and so can't live on the
/// entity itself): the names (valid languages, a default-language name, unique per language
/// among non-deleted types), and refusing to delete a type that's still assigned to a Space.
/// </summary>
public class SpaceTypeManager : DomainService
{
    private const string English = "en";
    private const string Arabic = "ar";

    private readonly ISpaceTypeRepository _spaceTypeRepository;
    private readonly IRepository<Space, Guid> _spaceRepository;
    private readonly IDataFilter _dataFilter;
    private readonly LocalizedNameValidator _nameValidator;

    public SpaceTypeManager(
        ISpaceTypeRepository spaceTypeRepository,
        IRepository<Space, Guid> spaceRepository,
        IDataFilter dataFilter,
        LocalizedNameValidator nameValidator)
    {
        _spaceTypeRepository = spaceTypeRepository;
        _spaceRepository = spaceRepository;
        _dataFilter = dataFilter;
        _nameValidator = nameValidator;
    }

    public async Task<SpaceType> CreateAsync(IEnumerable<LocalizedName> names, IconKey iconKey)
    {
        var spaceType = new SpaceType(GuidGenerator.Create(), iconKey);
        await SetNamesAsync(spaceType, names);
        return spaceType;
    }

    /// <summary>
    /// Replaces the type's names with <paramref name="names"/>: languages not listed lose
    /// their name, so the edit form sends every name it shows.
    /// </summary>
    public async Task SetNamesAsync(SpaceType spaceType, IEnumerable<LocalizedName> names)
    {
        var wanted = await _nameValidator.NormalizeAsync(names);

        foreach (var name in wanted)
        {
            var current = spaceType.FindName(name.Language);
            if (current is null || SpaceTypeTranslation.Normalize(current) != SpaceTypeTranslation.Normalize(name.Name))
            {
                await EnsureNameIsUniqueAsync(name, spaceType.Id);
            }
        }

        var dropped = spaceType.Translations.Select(t => t.Language).Except(wanted.Select(n => n.Language)).ToList();
        foreach (var language in dropped)
        {
            spaceType.RemoveName(language);
        }

        foreach (var name in wanted)
        {
            spaceType.SetName(name.Language, name.Name);
        }
    }

    /// <summary>Checks the type can go and frees its names; the caller then deletes it.</summary>
    public async Task PrepareForDeletionAsync(SpaceType spaceType)
    {
        await EnsureNotInUseAsync(spaceType.Id);
        spaceType.ReleaseNames();
    }

    public async Task EnsureNotInUseAsync(Guid spaceTypeId)
    {
        // Soft-deleted spaces count too: they can be restored, and a restored space whose type
        // is gone crashes every lookup of it. The default query filter would hide them.
        using var _ = _dataFilter.Disable<ISoftDelete>();
        if (await _spaceRepository.AnyAsync(s => s.SpaceTypeId == spaceTypeId))
        {
            throw new BusinessException(DixelsDomainErrorCodes.SpaceTypeInUse);
        }
    }

    /// <summary>
    /// The built-in type, created if it's missing (found by its English name). Its Arabic
    /// name is added when it has none yet — never overwriting one an admin changed, and never
    /// taking a name another type already uses. Saved at once: the hierarchy seeder asks for
    /// the same types, and contributors run in no guaranteed order — an unsaved insert would
    /// be invisible to the other's query, and both would insert the same name.
    /// </summary>
    public async Task<SpaceType> EnsureBuiltInAsync(BuiltInSpaceType builtIn)
    {
        var spaceType = await _spaceTypeRepository.FindByNameAsync(English, builtIn.EnglishName);
        var isNew = spaceType is null;
        spaceType ??= new SpaceType(GuidGenerator.Create(), English, builtIn.EnglishName, builtIn.IconKey);

        var addArabic = _nameValidator.IsAppLanguage(Arabic)
            && spaceType.FindName(Arabic) is null
            && !await _spaceTypeRepository.NameExistsAsync(Arabic, builtIn.ArabicName, spaceType.Id);
        if (addArabic)
        {
            spaceType.SetName(Arabic, builtIn.ArabicName);
        }

        if (isNew)
        {
            return await _spaceTypeRepository.InsertAsync(spaceType, autoSave: true);
        }

        return addArabic ? await _spaceTypeRepository.UpdateAsync(spaceType, autoSave: true) : spaceType;
    }

    private async Task EnsureNameIsUniqueAsync(LocalizedName name, Guid excludingId)
    {
        // The repository's default query filter already excludes soft-deleted types, matching
        // the partial unique index (WHERE "IsDeleted" = false) — a deleted type's name is free.
        if (await _spaceTypeRepository.NameExistsAsync(name.Language, name.Name, excludingId))
        {
            throw new BusinessException(DixelsDomainErrorCodes.SpaceTypeNameAlreadyExists)
                .WithData("name", name.Name)
                .WithData("language", name.Language);
        }
    }
}
