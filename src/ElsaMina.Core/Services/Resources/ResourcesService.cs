using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using ElsaMina.Core.Services.Config;

namespace ElsaMina.Core.Services.Resources;

public class ResourcesService : IResourcesService
{
    private readonly CultureInfo _defaultCulture;
    private readonly IReadOnlyList<ResourceManager> _resourceManagers;
    private readonly ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, string>>> _loadedCultures = new();
    private readonly Lazy<IReadOnlyList<CultureInfo>> _supportedCultures;

    public ResourcesService(IConfiguration configuration, IEnumerable<ResourceManager> resourceManagers)
    {
        _defaultCulture = new CultureInfo(configuration.DefaultLocaleCode);
        _resourceManagers = resourceManagers.ToList();
        _supportedCultures = new Lazy<IReadOnlyList<CultureInfo>>(
            GetSupportedCultures,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IEnumerable<CultureInfo> SupportedCultures => _supportedCultures.Value;

    public string GetString(string key, CultureInfo cultureInfo = null)
    {
        var culture = cultureInfo ?? _defaultCulture;
        var currentCulture = culture;
        while (!string.IsNullOrEmpty(currentCulture.Name))
        {
            var cultureStrings = GetCultureStrings(currentCulture.Name);
            if (cultureStrings.TryGetValue(key, out var localizedValue))
            {
                return localizedValue;
            }

            currentCulture = currentCulture.Parent;
        }

        var invariantStrings = GetCultureStrings(string.Empty);
        return invariantStrings.TryGetValue(key, out var invariantValue) ? invariantValue : key;
    }

    private IReadOnlyDictionary<string, string> GetCultureStrings(string cultureName)
    {
        return _loadedCultures.GetOrAdd(
            cultureName,
            name => new Lazy<IReadOnlyDictionary<string, string>>(
                () => LoadCultureStrings(name),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private IReadOnlyDictionary<string, string> LoadCultureStrings(string cultureName)
    {
        var culture = string.IsNullOrEmpty(cultureName)
            ? CultureInfo.InvariantCulture
            : new CultureInfo(cultureName);

        var strings = new Dictionary<string, string>();
        foreach (var manager in _resourceManagers)
        {
            var resourceSet = TryGetResourceSet(manager, culture);
            if (resourceSet == null)
            {
                continue;
            }

            foreach (DictionaryEntry entry in resourceSet)
            {
                if (entry.Value is not string stringValue)
                {
                    continue;
                }

                strings.TryAdd(entry.Key.ToString()!, stringValue);
            }
        }

        return strings;
    }

    private IReadOnlyList<CultureInfo> GetSupportedCultures()
    {
        var supportedLocales = new HashSet<CultureInfo>();
        var candidateCultures = new List<CultureInfo> { CultureInfo.InvariantCulture };
        candidateCultures.AddRange(CultureInfo.GetCultures(CultureTypes.AllCultures));

        foreach (var culture in candidateCultures)
        {
            foreach (var manager in _resourceManagers)
            {
                var resourceSet = TryGetResourceSet(manager, culture);
                if (resourceSet != null)
                {
                    supportedLocales.Add(string.IsNullOrEmpty(culture.Name) ? CultureInfo.InvariantCulture : culture);
                    break;
                }
            }
        }

        return supportedLocales.ToList();
    }

    private static ResourceSet TryGetResourceSet(ResourceManager manager, CultureInfo culture)
    {
        try
        {
            return manager.GetResourceSet(culture, true, false);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
        catch (MissingSatelliteAssemblyException)
        {
            return null;
        }
    }
}