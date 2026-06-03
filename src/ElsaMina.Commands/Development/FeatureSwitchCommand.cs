using System.Collections.Immutable;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.FeatureSwitches;

namespace ElsaMina.Commands.Development;

[NamedCommand("featureswitch", Aliases = ["fs"])]
public class FeatureSwitchCommand : DevelopmentCommand
{
    private readonly IFeatureSwitchService _featureSwitchService;

    public FeatureSwitchCommand(IFeatureSwitchService featureSwitchService)
    {
        _featureSwitchService = featureSwitchService;
    }

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var featureName = context.Target.Trim();
        if (string.IsNullOrEmpty(featureName))
        {
            var disabledFeatures = _featureSwitchService.DisabledFeatures.ToImmutableArray();
            context.ReplyLocalizedMessage(
                disabledFeatures.Length == 0 ? "featureswitch_none_disabled" : "featureswitch_disabled_list",
                string.Join(", ", disabledFeatures));
            return Task.CompletedTask;
        }

        var enable = !_featureSwitchService.IsFeatureEnabled(featureName);
        _featureSwitchService.SetFeature(featureName, enable);
        context.ReplyLocalizedMessage(
            enable ? "featureswitch_enabled" : "featureswitch_disabled",
            featureName);
        return Task.CompletedTask;
    }
}
