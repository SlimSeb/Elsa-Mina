using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.VoltorbFlip;

[NamedCommand("voltorbflip", Aliases = ["vf", "voltorb-flip"])]
public class StartVoltorbFlipCommand : GameCommand
{
    private readonly Func<VoltorbFlipGame> _gameFactory;
    private readonly IRoomsManager _roomsManager;
    private readonly IVoltorbFlipGameManager _gameManager;
    private readonly IArcadeEventsService _arcadeEventsService;

    public StartVoltorbFlipCommand(Func<VoltorbFlipGame> gameFactory,
        IRoomsManager roomsManager,
        IVoltorbFlipGameManager gameManager,
        IArcadeEventsService arcadeEventsService)
    {
        _gameFactory = gameFactory;
        _roomsManager = roomsManager;
        _gameManager = gameManager;
        _arcadeEventsService = arcadeEventsService;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (context.IsPrivateMessage)
        {
            await HandlePrivateMessageAsync(context);
            return;
        }

        await HandleRoomMessageAsync(context);
    }

    private async Task HandlePrivateMessageAsync(IContext context)
    {
        var roomId = context.Target?.Trim();
        if (string.IsNullOrEmpty(roomId))
        {
            context.ReplyLocalizedMessage("vf_game_pm_missing_room");
            return;
        }

        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            context.ReplyLocalizedMessage("vf_game_pm_invalid_room");
            return;
        }

        context.Culture = room.Culture;

        var userId = context.Sender.UserId;
        var existingGame = _gameManager.GetGame(roomId, userId);
        if (existingGame != null)
        {
            if (existingGame.IsRoundActive)
            {
                context.ReplyLocalizedMessage("vf_game_round_active");
                return;
            }

            existingGame.Context = context;
            await existingGame.StartNewRound();
            return;
        }

        var game = _gameFactory();
        game.Context = context;
        game.Owner = context.Sender;
        game.IsPrivateMode = true;
        game.TargetRoomId = roomId;
        game.TargetUserId = userId;
        _gameManager.RegisterGame(roomId, userId, game);
        await game.StartNewRound();
    }

    private async Task HandleRoomMessageAsync(IContext context)
    {
        if (_arcadeEventsService.AreGamesMuted(context.RoomId))
        {
            context.ReplyLocalizedMessage("games_muted_event");
            return;
        }

        var room = context.Room;

        if (room.Game is IVoltorbFlipGame voltorbFlip)
        {
            if (!voltorbFlip.IsStarted)
            {
                context.ReplyLocalizedMessage("vf_game_waiting");
                return;
            }

            if (voltorbFlip.IsRoundActive)
            {
                context.ReplyLocalizedMessage("vf_game_round_active");
                return;
            }

            voltorbFlip.Owner = context.Sender;
            voltorbFlip.Context = context;
            await voltorbFlip.StartNewRound();
            return;
        }

        if (room.Game != null)
        {
            context.ReplyLocalizedMessage("vf_game_already_running");
            return;
        }

        var game = _gameFactory();
        game.Context = context;
        room.Game = game;
        await game.DisplayAnnounce();
    }
}
