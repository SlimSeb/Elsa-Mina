using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Semantix;

[NamedCommand("semantix", Aliases = ["sx"])]
public class StartSemantixCommand : Command
{
    private readonly IDependencyContainerService _dependencyContainerService;
    private readonly IRoomsManager _roomsManager;
    private readonly ISemantixGameManager _gameManager;
    private readonly ISemantixDailyService _dailyService;
    private readonly IArcadeEventsService _arcadeEventsService;

    public StartSemantixCommand(IDependencyContainerService dependencyContainerService,
        IRoomsManager roomsManager,
        ISemantixGameManager gameManager,
        ISemantixDailyService dailyService,
        IArcadeEventsService arcadeEventsService)
    {
        _dependencyContainerService = dependencyContainerService;
        _roomsManager = roomsManager;
        _gameManager = gameManager;
        _dailyService = dailyService;
        _arcadeEventsService = arcadeEventsService;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "sx_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        string roomId;
        bool isPrivateMode;

        if (context.IsPrivateMessage)
        {
            roomId = context.Target?.Trim();
            if (string.IsNullOrEmpty(roomId))
            {
                context.ReplyLocalizedMessage("sx_pm_missing_room");
                return;
            }

            var room = _roomsManager.GetRoom(roomId);
            if (room == null)
            {
                context.ReplyLocalizedMessage("sx_pm_invalid_room");
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

        if (await _dailyService.HasWonTodayAsync(userId, cancellationToken))
        {
            context.ReplyLocalizedMessage("sx_already_won_today");
            return;
        }

        var game = _dependencyContainerService.Resolve<SemantixGame>();
        game.Context = context;
        game.Owner = context.Sender;
        game.IsPrivateMode = isPrivateMode;
        game.TargetRoomId = roomId;
        game.TargetUserId = userId;

        if (await game.StartNewRound())
        {
            _gameManager.RegisterGame(roomId, userId, game);
        }
    }
}
