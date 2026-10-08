using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Localization;
using Volo.Abp.MultiLingualObjects;
using Volo.Abp.Settings;

namespace Dixels.Localization;

/// <summary>
/// Which of an entity's names a reader sees — their language (Accept-Language), else its
/// parent (ar-JO → ar), else the default language's, which every named entity has. In
/// memory that's ABP's IMultiLingualObjectManager; for a database query (sorting a page by
/// name) <see cref="GetLanguagesAsync"/> gives the two languages to look for.
/// </summary>
public class LocalizedNameReader : ITransientDependency
{
    private readonly IMultiLingualObjectManager _multiLingualObjectManager;
    private readonly IOptions<AbpLocalizationOptions> _localizationOptions;
    private readonly ISettingProvider _settingProvider;

    public LocalizedNameReader(
        IMultiLingualObjectManager multiLingualObjectManager,
        IOptions<AbpLocalizationOptions> localizationOptions,
        ISettingProvider settingProvider)
    {
        _multiLingualObjectManager = multiLingualObjectManager;
        _localizationOptions = localizationOptions;
        _settingProvider = settingProvider;
    }

    /// <summary>The name to show for one entity (its names must be loaded).</summary>
    public async Task<string> ShownAsync<TTranslation>(IMultiLingualObject<TTranslation> entity)
        where TTranslation : NameTranslation
    {
        return (await ShownTranslationAsync(entity))?.Name ?? string.Empty;
    }

    /// <summary>The translation whose name is shown — for what else it holds (a building's address).</summary>
    public Task<TTranslation?> ShownTranslationAsync<TTranslation>(IMultiLingualObject<TTranslation> entity)
        where TTranslation : NameTranslation
    {
        return _multiLingualObjectManager.GetTranslationAsync<IMultiLingualObject<TTranslation>, TTranslation>(entity);
    }

    /// <summary>The name to show for each entity, by id, worked out in one pass.</summary>
    public async Task<Dictionary<Guid, string>> ShownAsync<TEntity, TTranslation>(IEnumerable<TEntity> entities)
        where TEntity : IMultiLingualObject<TTranslation>, IEntity<Guid>
        where TTranslation : NameTranslation
    {
        var named = await _multiLingualObjectManager.GetBulkTranslationsAsync<TEntity, TTranslation>(entities.DistinctBy(e => e.Id));
        return named.ToDictionary(pair => pair.entity.Id, pair => pair.translation?.Name ?? string.Empty);
    }

    /// <summary>
    /// For a query: the app language matching the reader's culture (or its parent), and the
    /// default language to fall back to — "the name in Shown, else the one in Fallback".
    /// </summary>
    public async Task<(string Shown, string Fallback)> GetLanguagesAsync()
    {
        var fallback = await _settingProvider.GetOrNullAsync(LocalizationSettingNames.DefaultLanguage) ?? "en";
        var languages = _localizationOptions.Value.Languages;
        for (var culture = CultureInfo.CurrentUICulture; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent)
        {
            var match = languages.FirstOrDefault(l => string.Equals(l.CultureName, culture.Name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return (match.CultureName, fallback);
            }
        }

        return (fallback, fallback);
    }
}
