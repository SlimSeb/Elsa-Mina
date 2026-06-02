using System.Collections.Concurrent;

namespace ElsaMina.Core.Services.FeatureSwitches;

public class FeatureSwitchService : IFeatureSwitchService
{
    private volatile bool _isMaydayActive;
    private readonly ConcurrentDictionary<string, bool> _featureSwitches = new();

    public bool IsMaydayActive => _isMaydayActive;

    public void SetMayday(bool active)
    {
        _isMaydayActive = active;
    }

    public bool IsFeatureEnabled(string featureName)
    {
        return _featureSwitches.GetValueOrDefault(featureName, true);
    }

    public void SetFeature(string featureName, bool enabled)
    {
        _featureSwitches[featureName] = enabled;
    }

    public IEnumerable<string> DisabledFeatures => _featureSwitches
        .Where(kv => !kv.Value)
        .Select(kv => kv.Key);
}