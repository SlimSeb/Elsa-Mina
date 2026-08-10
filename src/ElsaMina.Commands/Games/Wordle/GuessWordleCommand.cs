using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Wordle;

/// <summary>
/// Submits a full word to the sender's Wordle game. Deliberately private message only: a guess typed in
/// the room would spoil the daily word for everyone watching, so the board's input field sends it as a
/// whisper to the bot and any answer is whispered back.
/// </summary>
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
    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "wordle_guess_pm_format";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = (context.Target ?? string.Empty).Split(',', 2);
        if (parts.Length < 2)
        {
            context.ReplyLocalizedMessage("wordle_guess_pm_format");
            return;
        }

        var roomId = parts[0].Trim();
        var word = parts[1].Trim();

        var room = _roomsManager.GetRoom(roomId);
        if (room != null)
        {
            context.Culture = room.Culture;
        }

        var wordle = _gameManager.GetGame(roomId, context.Sender.UserId);
        if (wordle == null)
        {
            context.ReplyLocalizedMessage("wordle_game_no_game");
            return;
        }

        if (wordle.IsPrivateMode)
        {
            wordle.Context = context;
        }

        var outcome = await wordle.SubmitWord(context.Sender, word);
        switch (outcome)
        {
            case WordleGuessOutcome.RoundNotActive:
                context.ReplyLocalizedMessage("wordle_guess_round_not_active");
                break;
            case WordleGuessOutcome.NotOwner:
                context.ReplyLocalizedMessage("wordle_guess_not_owner");
                break;
        }
    }
}
