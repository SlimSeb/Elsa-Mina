using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Wordle;

[NamedCommand("wordleguess", Aliases = ["wlg"])]
public class GuessWordleCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly IWordleGameManager _gameManager;

    public GuessWordleCommand(IRoomsManager roomsManager, IWordleGameManager gameManager)
    {
        _roomsManager = roomsManager;
        _gameManager = gameManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        IWordleGame wordle;
        string word;

        if (context.IsPrivateMessage)
        {
            var parts = context.Target.Split(',', 2);
            if (parts.Length < 2)
            {
                context.ReplyLocalizedMessage("wordle_guess_pm_format");
                return;
            }

            var roomId = parts[0].Trim();
            word = parts[1].Trim();

            var room = _roomsManager.GetRoom(roomId);
            if (room != null)
            {
                context.Culture = room.Culture;
            }

            wordle = _gameManager.GetGame(roomId, context.Sender.UserId);

            if (wordle is { IsPrivateMode: true })
            {
                wordle.Context = context;
            }
        }
        else
        {
            word = context.Target?.Trim();
            wordle = _gameManager.GetGame(context.RoomId, context.Sender.UserId);
        }

        if (wordle == null)
        {
            context.ReplyLocalizedMessage("wordle_game_no_game");
            return;
        }

        var outcome = await wordle.SubmitGuess(context.Sender, word);
        switch (outcome)
        {
            case WordleGuessOutcome.RoundNotActive:
                context.ReplyLocalizedMessage("wordle_guess_round_not_active");
                break;
            case WordleGuessOutcome.NotOwner:
                context.ReplyLocalizedMessage("wordle_guess_not_owner");
                break;
            case WordleGuessOutcome.InvalidLength:
                context.ReplyLocalizedMessage("wordle_guess_invalid_length", wordle.WordLength);
                break;
            case WordleGuessOutcome.NotAlphabetic:
                context.ReplyLocalizedMessage("wordle_guess_not_alphabetic");
                break;
            case WordleGuessOutcome.NotInWordList:
                context.ReplyLocalizedMessage("wordle_guess_not_in_list");
                break;
            case WordleGuessOutcome.AlreadyGuessed:
                context.ReplyLocalizedMessage("wordle_guess_already_guessed");
                break;
        }
    }
}
