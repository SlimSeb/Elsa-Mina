using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.FeatureSwitches;

namespace ElsaMina.Commands.Development;

[NamedCommand("mayday")]
public class MaydayCommand : DevelopmentCommand
{
    private readonly IFeatureSwitchService _featureSwitchService;

    public MaydayCommand(IFeatureSwitchService featureSwitchService)
    {
        _featureSwitchService = featureSwitchService;
    }

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var activate = !_featureSwitchService.IsMaydayActive;
        _featureSwitchService.SetMayday(activate);
        context.ReplyLocalizedMessage(activate ? "mayday_activated" : "mayday_deactivated");
        return Task.CompletedTask;
    }
}
