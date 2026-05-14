using System.Collections.Concurrent;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Games.Blackjack;

[NamedCommand("blackjack", Aliases = ["bj"])]
public class BlackjackCommand : Command
{
    internal static readonly ConcurrentDictionary<string, BlackjackGame> ACTIVE_GAMES = new();

    private readonly IDependencyContainerService _dependencyContainerService;

    public BlackjackCommand(IDependencyContainerService dependencyContainerService)
    {
        _dependencyContainerService = dependencyContainerService;
    }

    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var key = GameKey(context.RoomId, context.Sender.UserId);

        if (ACTIVE_GAMES.ContainsKey(key))
        {
            context.ReplyLocalizedMessage("bj_game_already_active");
            return;
        }

        var game = _dependencyContainerService.Resolve<BlackjackGame>();
        game.Player = context.Sender;
        game.Context = context;
        ACTIVE_GAMES[key] = game;

        await game.DisplayTableAsync();

        if (game.IsOver)
        {
            ACTIVE_GAMES.TryRemove(key, out _);
        }
    }

    internal static string GameKey(string roomId, string userId) => $"{roomId}:{userId}";
}
