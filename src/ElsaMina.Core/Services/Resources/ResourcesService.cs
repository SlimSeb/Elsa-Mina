using System.Globalization;
using System.Resources;
using ElsaMina.Core.Services.Config;

namespace ElsaMina.Core.Services.Resources;

public class ResourcesService : IResourcesService
{
    private readonly CultureInfo _defaultCulture;
    private readonly IReadOnlyList<ResourceManager> _resourceManagers;
    private IEnumerable<CultureInfo> _supportedCultures;

    public ResourcesService(IConfiguration configuration, IEnumerable<ResourceManager> resourceManagers)
    {
        _defaultCulture = new CultureInfo(configuration.DefaultLocaleCode);
        _resourceManagers = resourceManagers.ToList();
    }

    public IEnumerable<CultureInfo> SupportedCultures => _supportedCultures ??= GetSupportedCultures();

    public string GetString(string key, CultureInfo cultureInfo = null)
    {
        var culture = cultureInfo ?? _defaultCulture;
        foreach (var manager in _resourceManagers)
        {
            try
            {
                var value = manager.GetString(key, culture);
                if (value != null)
                {
                    return value;
                }
            }
            catch (MissingManifestResourceException)
            {
                // Key not in this manager — try the next one
            }
        }

        return key;
    }

    private List<CultureInfo> GetSupportedCultures()
    {
        var supportedLocales = new HashSet<CultureInfo>();
        foreach (var manager in _resourceManagers)
        {
            foreach (var cultureInfo in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                try
                {
                    var resourceSet = manager.GetResourceSet(cultureInfo, true, false);
                    if (resourceSet != null)
                    {
                        supportedLocales.Add(cultureInfo);
                    }
                }
                catch (CultureNotFoundException)
                {
                    // Do nothing
                }
            }
        }

        return supportedLocales.ToList();
    }
}