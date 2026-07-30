using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Dolls;

[NamedCommand("refreshdolls", Aliases = ["refresh-dolls", "syncdolls", "sync-dolls"])]
public class RefreshDollsCommand : Command
{
    private readonly IDollService _dollService;

    public RefreshDollsCommand(IDollService dollService)
    {
        _dollService = dollService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "doll_refresh_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var catalogue = await _dollService.RefreshCatalogueAsync(cancellationToken);
            context.ReplyLocalizedMessage("doll_refresh_success", catalogue.Count);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Could not refresh the doll catalogue");
            context.ReplyLocalizedMessage("doll_refresh_failure", exception.Message);
        }
    }
}
