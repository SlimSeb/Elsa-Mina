using System.Collections;
using System.Globalization;
using System.Resources;
using ElsaMina.Core.Services.Config;

namespace ElsaMina.Core.Services.Resources;

public class ResourcesService : IResourcesService
{
    private readonly CultureInfo _defaultCulture;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _mergedResources;

    public ResourcesService(IConfiguration configuration, IEnumerable<ResourceManager> resourceManagers)
    {
        _defaultCulture = new CultureInfo(configuration.DefaultLocaleCode);
        _mergedResources = BuildMergedResources(resourceManagers);
    }

    public IEnumerable<CultureInfo> SupportedCultures => _mergedResources.Values
        .SelectMany(map => map.Keys)
        .Distinct()
        .Select(name => string.IsNullOrEmpty(name) ? CultureInfo.InvariantCulture : new CultureInfo(name));

    public string GetString(string key, CultureInfo cultureInfo = null)
    {
        if (!_mergedResources.TryGetValue(key, out var cultureMap))
        {
            return key;
        }

        var culture = cultureInfo ?? _defaultCulture;
        var current = culture;
        while (!string.IsNullOrEmpty(current.Name))
        {
            if (cultureMap.TryGetValue(current.Name, out var value))
            {
                return value;
            }

            current = current.Parent;
        }

        return cultureMap.TryGetValue(string.Empty, out var invariantValue) ? invariantValue : key;
    }

    private static Dictionary<string, IReadOnlyDictionary<string, string>> BuildMergedResources(
        IEnumerable<ResourceManager> resourceManagers)
    {
        var merged = new Dictionary<string, Dictionary<string, string>>();
        foreach (var manager in resourceManagers)
        {
            foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                ResourceSet resourceSet;
                try
                {
                    resourceSet = manager.GetResourceSet(culture, true, false);
                }
                catch (CultureNotFoundException)
                {
                    continue;
                }

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

                    var entryKey = entry.Key.ToString()!;
                    if (!merged.TryGetValue(entryKey, out var cultureMap))
                    {
                        merged[entryKey] = cultureMap = new Dictionary<string, string>();
                    }

                    cultureMap.TryAdd(culture.Name, stringValue);
                }
            }
        }

        return merged.ToDictionary(
            kvp => kvp.Key,
            IReadOnlyDictionary<string, string> (kvp) => kvp.Value);
    }
}