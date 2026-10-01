using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.ConnectFour;

[NamedCommand("connectfour", Aliases = ["connect-four", "c4", "connect4"])]
public class CreateConnectFourCommand : GameCommand
{
    private readonly Func<ConnectFourGame> _gameFactory;
    private readonly IEventAnnouncer _eventAnnouncer;

    public CreateConnectFourCommand(Func<ConnectFourGame> gameFactory,
        IEventAnnouncer eventAnnouncer)
    {
        _gameFactory = gameFactory;
        _eventAnnouncer = eventAnnouncer;
    }

    public override Rank RequiredRank => Rank.Voiced;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var room = context.Room;
        if (room.Game is not null)
        {
            context.ReplyLocalizedMessage("c4_game_start_already_exist");
            return;
        }

        var game = _gameFactory();
        game.Context = context;
        room.Game = game;

        await _eventAnnouncer.AnnounceToLinkedRoomsAsync(context.RoomId, EventAnnounceType.Game,
            "connect_four_started_in", [context.RoomId], cancellationToken);

        await game.DisplayAnnounce();
    }
}