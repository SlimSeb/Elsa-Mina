using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Semantix;

[NamedCommand("sxguess", Aliases = ["sxg"])]
public class GuessSemantixCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly ISemantixGameManager _gameManager;

    public GuessSemantixCommand(IRoomsManager roomsManager, ISemantixGameManager gameManager)
    {
        _roomsManager = roomsManager;
        _gameManager = gameManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "sx_guess_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        ISemantixGame semantix;
        string word;

        if (context.IsPrivateMessage)
        {
            var parts = context.Target.Split(',', 2);
            if (parts.Length < 2)
            {
                context.ReplyLocalizedMessage("sx_guess_pm_format");
                return;
            }

            var roomId = parts[0].Trim();
            word = parts[1].Trim();

            var room = _roomsManager.GetRoom(roomId);
            if (room != null)
            {
                context.Culture = room.Culture;
            }

            semantix = _gameManager.GetGame(roomId, context.Sender.UserId);
            if (semantix is { IsPrivateMode: true })
            {
                semantix.Context = context;
            }
        }
        else
        {
            word = context.Target?.Trim();
            semantix = _gameManager.GetGame(context.RoomId, context.Sender.UserId);
        }

        if (semantix == null)
        {
            context.ReplyLocalizedMessage("sx_game_no_game");
            return;
        }

        var outcome = await semantix.SubmitGuess(context.Sender, word);
        switch (outcome)
        {
            case SemantixGuessOutcome.RoundNotActive:
                context.ReplyLocalizedMessage("sx_guess_round_not_active");
                break;
            case SemantixGuessOutcome.NotOwner:
                context.ReplyLocalizedMessage("sx_guess_not_owner");
                break;
            case SemantixGuessOutcome.EmptyWord:
                context.ReplyLocalizedMessage("sx_guess_empty");
                break;
            case SemantixGuessOutcome.NotInWordList:
                context.ReplyLocalizedMessage("sx_guess_not_in_list");
                break;
            case SemantixGuessOutcome.AlreadyGuessed:
                context.ReplyLocalizedMessage("sx_guess_already_guessed");
                break;
            case SemantixGuessOutcome.EmbeddingUnavailable:
                context.ReplyLocalizedMessage("sx_game_api_unavailable");
                break;
        }
    }
}
