using ElsaMina.Commands.Users.RoomUserData;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.JoinPhrases;

[NamedCommand("setjoinphrase",
    Aliases = ["deletejoinphrase", "delete-joinphrase", "removejoinphrase", "remove-joinphrase"])]
public class SetJoinPhraseCommand : Command
{
    private readonly IRoomUserDataService _roomUserDataService;

    public SetJoinPhraseCommand(IRoomUserDataService roomUserDataService)
    {
        _roomUserDataService = roomUserDataService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override string HelpMessageKey => "setjoinphrase_help_message";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string userId;
        string joinPhrase;
        if (context.Command is "deletejoinphrase" or "delete-joinphrase" or "removejoinphrase" or "remove-joinphrase")
        {
            userId = context.Target.ToLowerAlphaNum();
            joinPhrase = string.Empty;
        }
        else
        {
            var parts = context.Target.Split(',');
            if (parts.Length != 2)
            {
                ReplyLocalizedHelpMessage(context);
                return;
            }

            userId = parts[0].ToLowerAlphaNum();
            joinPhrase = parts[1].Trim();
        }

        try
        {
            await _roomUserDataService.SetUserJoinPhraseAsync(context.RoomId, userId, joinPhrase, cancellationToken);
            context.ReplyLocalizedMessage("setjoinphrase_success");
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Error while updating join phrase");
            context.ReplyLocalizedMessage("setjoinphrase_failure", exception.Message);
        }
    }
}