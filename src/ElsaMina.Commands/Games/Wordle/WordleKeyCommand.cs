using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Wordle;

[NamedCommand("wlkey")]
public class WordleKeyCommand : Command
{
    private const string BACKSPACE_ACTION = "back";
    private const string ENTER_ACTION = "enter";

    private readonly IRoomsManager _roomsManager;
    private readonly IWordleGameManager _gameManager;

    public WordleKeyCommand(IRoomsManager roomsManager, IWordleGameManager gameManager)
    {
        _roomsManager = roomsManager;
        _gameManager = gameManager;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',', 2);
        if (parts.Length < 2)
        {
            return;
        }

        var roomId = parts[0].Trim();
        var action = parts[1].Trim();
        if (string.IsNullOrEmpty(action))
        {
            return;
        }

        var wordle = _gameManager.GetGame(roomId, context.Sender.UserId);
        if (wordle == null)
        {
            return;
        }

        if (wordle.IsPrivateMode)
        {
            var room = _roomsManager.GetRoom(roomId);
            if (room != null)
            {
                context.Culture = room.Culture;
            }

            wordle.Context = context;
        }

        switch (action.ToLowerInvariant())
        {
            case BACKSPACE_ACTION:
                await wordle.RemoveLetter(context.Sender);
                break;
            case ENTER_ACTION:
                await wordle.SubmitCurrentInput(context.Sender);
                break;
            default:
                if (action.Length == 1)
                {
                    await wordle.AppendLetter(context.Sender, action[0]);
                }
                break;
        }
    }
}
