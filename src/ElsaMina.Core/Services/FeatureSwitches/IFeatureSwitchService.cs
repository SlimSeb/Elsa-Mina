namespace ElsaMina.Core.Services.FeatureSwitches;

public interface IFeatureSwitchService
{
    bool IsMaydayActive { get; }
    void SetMayday(bool active);

    bool IsFeatureEnabled(string featureName);
    void SetFeature(string featureName, bool enabled);
    IEnumerable<string> DisabledFeatures { get; }
}
