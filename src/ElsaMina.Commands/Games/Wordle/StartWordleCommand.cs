using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Wordle;

[NamedCommand("wordle", Aliases = ["wl"])]
public class StartWordleCommand : Command
{
    private readonly IDependencyContainerService _dependencyContainerService;
    private readonly IRoomsManager _roomsManager;
    private readonly IWordleGameManager _gameManager;
    private readonly IWordleDailyService _dailyService;
    private readonly IArcadeEventsService _arcadeEventsService;

    public StartWordleCommand(IDependencyContainerService dependencyContainerService,
        IRoomsManager roomsManager,
        IWordleGameManager gameManager,
        IWordleDailyService dailyService,
        IArcadeEventsService arcadeEventsService)
    {
        _dependencyContainerService = dependencyContainerService;
        _roomsManager = roomsManager;
        _gameManager = gameManager;
        _dailyService = dailyService;
        _arcadeEventsService = arcadeEventsService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string roomId;
        bool isPrivateMode;

        if (context.IsPrivateMessage)
        {
            roomId = context.Target?.Trim();
            if (string.IsNullOrEmpty(roomId))
            {
                context.ReplyLocalizedMessage("wordle_pm_missing_room");
                return;
            }

            var room = _roomsManager.GetRoom(roomId);
            if (room == null)
            {
                context.ReplyLocalizedMessage("wordle_pm_invalid_room");
                return;
            }

            context.Culture = room.Culture;
            isPrivateMode = true;
        }
        else
        {
            if (_arcadeEventsService.AreGamesMuted(context.RoomId))
            {
                context.ReplyLocalizedMessage("games_muted_event");
                return;
            }

            roomId = context.RoomId;
            isPrivateMode = false;
        }

        var userId = context.Sender.UserId;

        var existingGame = _gameManager.GetGame(roomId, userId);
        if (existingGame is { IsEnded: false })
        {
            existingGame.Context = context;
            await existingGame.ResumeAsync();
            return;
        }

        var timeZone = _roomsManager.GetRoom(roomId)?.TimeZone ?? TimeZoneInfo.Utc;
        if (await _dailyService.HasPlayedTodayAsync(userId, timeZone, cancellationToken))
        {
            context.ReplyLocalizedMessage("wordle_already_played_today");
            return;
        }

        var game = _dependencyContainerService.Resolve<WordleGame>();
        game.Context = context;
        game.Owner = context.Sender;
        game.IsPrivateMode = isPrivateMode;
        game.TargetRoomId = roomId;
        game.TargetUserId = userId;
        _gameManager.RegisterGame(roomId, userId, game);
        await game.StartNewRound();
    }
}
