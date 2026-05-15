using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Blackjack;

[NamedCommand("blackjack", Aliases = ["bj"])]
public class BlackjackCommand : Command
{
    private readonly IDependencyContainerService _dependencyContainerService;
    private readonly IRoomsManager _roomsManager;
    private readonly IBlackjackGameManager _gameManager;
    private readonly IArcadeEventsService _arcadeEventsService;

    public BlackjackCommand(
        IDependencyContainerService dependencyContainerService,
        IRoomsManager roomsManager,
        IBlackjackGameManager gameManager,
        IArcadeEventsService arcadeEventsService)
    {
        _dependencyContainerService = dependencyContainerService;
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
            context.ReplyLocalizedMessage("bj_pm_missing_room");
            return;
        }

        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            context.ReplyLocalizedMessage("bj_pm_invalid_room");
            return;
        }

        context.Culture = room.Culture;

        var userId = context.Sender.UserId;
        if (_gameManager.GetGame(roomId, userId) != null)
        {
            context.ReplyLocalizedMessage("bj_game_already_active");
            return;
        }

        var game = _dependencyContainerService.Resolve<BlackjackGame>();
        game.Context = context;
        game.Owner = context.Sender;
        game.IsPrivateMode = true;
        game.TargetRoomId = roomId;
        game.TargetUserId = userId;
        _gameManager.RegisterGame(roomId, userId, game);
        await game.StartGame();
    }

    private async Task HandleRoomMessageAsync(IContext context)
    {
        if (_arcadeEventsService.AreGamesMuted(context.RoomId))
        {
            context.ReplyLocalizedMessage("games_muted_event");
            return;
        }

        var room = context.Room;

        if (room.Game is IBlackjackGame)
        {
            context.ReplyLocalizedMessage("bj_game_already_running");
            return;
        }

        if (room.Game != null)
        {
            context.ReplyLocalizedMessage("bj_game_already_running");
            return;
        }

        var game = _dependencyContainerService.Resolve<BlackjackGame>();
        game.Context = context;
        room.Game = game;
        await game.DisplayAnnounce();
    }
}
