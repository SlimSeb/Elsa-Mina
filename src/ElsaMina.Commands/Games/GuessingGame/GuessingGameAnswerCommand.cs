using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.GuessingGame;

[NamedCommand("answer", Aliases = ["guess", "guessinggameanswer"])]
public class GuessingGameAnswerCommand : GameCommand
{
    private readonly IRoomsManager _roomsManager;

    public GuessingGameAnswerCommand(IRoomsManager roomsManager)
    {
        _roomsManager = roomsManager;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override bool IsPrivateMessageOnly => true;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(",");
        if (parts.Length != 2)
        {
            return Task.CompletedTask; // ignore
        }

        var roomId = parts[0].ToLowerAlphaNum();
        var response = string.Join(",", parts.Skip(1));

        var room = _roomsManager.GetRoom(roomId);
        if (room?.Game is IGuessingGame guessingGame)
        {
            guessingGame.OnAnswer(context.Sender.Name, response);
        }
        
        return Task.CompletedTask;
    }
}