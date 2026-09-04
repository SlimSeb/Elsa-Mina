using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Tournaments.Betting;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Commands.RoomDashboard;

[NamedCommand("room-config", Aliases = ["roomconfig", "rc"])]
public class RoomConfigCommand : Command
{
    private const int DEFAULT_MUTE_GAMES_MINUTES = 30;

    private readonly IRoomsManager _roomsManager;
    private readonly IParametersDefinitionFactory _parametersDefinitionFactory;
    private readonly IArcadeEventsService _arcadeEventsService;
    private readonly ITournamentBettingService _tournamentBettingService;
    private readonly IRoomDashboardService _roomDashboardService;

    public RoomConfigCommand(
        IRoomsManager roomsManager,
        IParametersDefinitionFactory parametersDefinitionFactory,
        IArcadeEventsService arcadeEventsService,
        ITournamentBettingService tournamentBettingService,
        IRoomDashboardService roomDashboardService)
    {
        _roomsManager = roomsManager;
        _parametersDefinitionFactory = parametersDefinitionFactory;
        _arcadeEventsService = arcadeEventsService;
        _tournamentBettingService = tournamentBettingService;
        _roomDashboardService = roomDashboardService;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            return;
        }

        var parts = context.Target.Split(",");
        var roomId = parts[0].Trim().ToLowerAlphaNum();

        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            context.ReplyLocalizedMessage("room_config_room_not_found", roomId);
            return;
        }

        if (!await context.HasSufficientRankInRoom(roomId, Rank.Driver, cancellationToken))
        {
            return;
        }

        if (context.IsPrivateMessage)
        {
            context.Culture = room.Culture;
        }

        var roomParameters = _parametersDefinitionFactory.GetParametersDefinitions();
        try
        {
            foreach (var rawPair in parts.Skip(1))
            {
                var pair = rawPair.Trim();
                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }

                if (await TryHandleQuickActionAsync(pair, room, roomId, roomParameters, cancellationToken))
                {
                    continue;
                }

                var items = pair.Split('=');
                if (items.Length != 2)
                {
                    context.ReplyLocalizedMessage("room_config_invalid_pair", pair);
                    return;
                }

                var parameterId = items[0].Trim();
                var value = items[1].Trim();
                var match = roomParameters
                    .FirstOrDefault(kvp => string.Equals(kvp.Value.Identifier, parameterId, StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(kvp.Key.ToString(), parameterId, StringComparison.OrdinalIgnoreCase));
                if (match.Value == null)
                {
                    context.ReplyLocalizedMessage("room_config_unknown_parameter", parameterId);
                    return;
                }

                var success = await room.SetParameterValueAsync(match.Key, value, cancellationToken);
                if (!success)
                {
                    context.ReplyLocalizedMessage("room_config_invalid_value", value, parameterId);
                    return;
                }
            }

            context.ReplyLocalizedMessage("room_config_success", roomId);
            await _roomDashboardService.SendDashboardPageAsync(context, roomId, cancellationToken);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "An error occurred while updating room configuration");
            context.ReplyLocalizedMessage("room_config_failure", exception.Message);
        }
    }

    private async Task<bool> TryHandleQuickActionAsync(
        string pair,
        IRoom room,
        string roomId,
        IReadOnlyDictionary<Parameter, IParameterDefinition> roomParameters,
        CancellationToken cancellationToken)
    {

        if (pair.Equals("mutegames", StringComparison.OrdinalIgnoreCase) ||
            pair.Equals("mute", StringComparison.OrdinalIgnoreCase))
        {
            _arcadeEventsService.MuteGames(roomId, TimeSpan.FromMinutes(DEFAULT_MUTE_GAMES_MINUTES));
            return true;
        }

        if (pair.Equals("unmutegames", StringComparison.OrdinalIgnoreCase) ||
            pair.Equals("unmute", StringComparison.OrdinalIgnoreCase))
        {
            _arcadeEventsService.UnmuteGames(roomId);
            return true;
        }

        if (pair.Equals("togglegames", StringComparison.OrdinalIgnoreCase))
        {
            if (_arcadeEventsService.AreGamesMuted(roomId))
            {
                _arcadeEventsService.UnmuteGames(roomId);
            }
            else
            {
                _arcadeEventsService.MuteGames(roomId, TimeSpan.FromMinutes(DEFAULT_MUTE_GAMES_MINUTES));
            }
            return true;
        }

        if (pair.Equals("cancelgame", StringComparison.OrdinalIgnoreCase) ||
            pair.Equals("endgame", StringComparison.OrdinalIgnoreCase))
        {
            await TryCancelActiveGameAsync(room);
            return true;
        }

        if (pair.Equals("cancelbets", StringComparison.OrdinalIgnoreCase) ||
            pair.Equals("clearbets", StringComparison.OrdinalIgnoreCase) ||
            pair.Equals("returnbets", StringComparison.OrdinalIgnoreCase))
        {
            await _tournamentBettingService.ReturnBetsAsync(roomId, cancellationToken);
            return true;
        }

        if (pair.Equals("reset", StringComparison.OrdinalIgnoreCase) ||
            pair.Equals("defaults", StringComparison.OrdinalIgnoreCase) ||
            pair.Equals("resetdefaults", StringComparison.OrdinalIgnoreCase))
        {
            await ResetParametersToDefaultsAsync(room, roomParameters, cancellationToken);
            return true;
        }

        if (pair.Contains('='))
        {
            var split = pair.Split('=', 2);
            var actionKey = split[0].Trim();
            var actionValue = split[1].Trim();

            if (actionKey.Equals("action", StringComparison.OrdinalIgnoreCase))
            {
                if (actionValue.Equals("mutegames", StringComparison.OrdinalIgnoreCase) ||
                    actionValue.Equals("mute", StringComparison.OrdinalIgnoreCase))
                {
                    _arcadeEventsService.MuteGames(roomId, TimeSpan.FromMinutes(DEFAULT_MUTE_GAMES_MINUTES));
                    return true;
                }

                if (actionValue.Equals("unmutegames", StringComparison.OrdinalIgnoreCase) ||
                    actionValue.Equals("unmute", StringComparison.OrdinalIgnoreCase))
                {
                    _arcadeEventsService.UnmuteGames(roomId);
                    return true;
                }

                if (actionValue.Equals("togglegames", StringComparison.OrdinalIgnoreCase))
                {
                    if (_arcadeEventsService.AreGamesMuted(roomId))
                    {
                        _arcadeEventsService.UnmuteGames(roomId);
                    }
                    else
                    {
                        _arcadeEventsService.MuteGames(roomId, TimeSpan.FromMinutes(DEFAULT_MUTE_GAMES_MINUTES));
                    }
                    return true;
                }

                if (actionValue.Equals("cancelgame", StringComparison.OrdinalIgnoreCase) ||
                    actionValue.Equals("endgame", StringComparison.OrdinalIgnoreCase))
                {
                    await TryCancelActiveGameAsync(room);
                    return true;
                }

                if (actionValue.Equals("cancelbets", StringComparison.OrdinalIgnoreCase) ||
                    actionValue.Equals("clearbets", StringComparison.OrdinalIgnoreCase) ||
                    actionValue.Equals("returnbets", StringComparison.OrdinalIgnoreCase))
                {
                    await _tournamentBettingService.ReturnBetsAsync(roomId, cancellationToken);
                    return true;
                }

                if (actionValue.Equals("reset", StringComparison.OrdinalIgnoreCase) ||
                    actionValue.Equals("defaults", StringComparison.OrdinalIgnoreCase) ||
                    actionValue.Equals("resetdefaults", StringComparison.OrdinalIgnoreCase))
                {
                    await ResetParametersToDefaultsAsync(room, roomParameters, cancellationToken);
                    return true;
                }
            }
            else if (actionKey.Equals("mutegames", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(actionValue, out var durationMinutes) && durationMinutes > 0)
                {
                    _arcadeEventsService.MuteGames(roomId, TimeSpan.FromMinutes(durationMinutes));
                    return true;
                }

                if (actionValue.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                    actionValue.Equals("off", StringComparison.OrdinalIgnoreCase))
                {
                    _arcadeEventsService.UnmuteGames(roomId);
                    return true;
                }

                _arcadeEventsService.MuteGames(roomId, TimeSpan.FromMinutes(DEFAULT_MUTE_GAMES_MINUTES));
                return true;
            }
            else if (actionKey.Equals("games", StringComparison.OrdinalIgnoreCase))
            {
                if (actionValue.Equals("mute", StringComparison.OrdinalIgnoreCase) ||
                    actionValue.Equals("muted", StringComparison.OrdinalIgnoreCase) ||
                    actionValue.Equals("disabled", StringComparison.OrdinalIgnoreCase))
                {
                    _arcadeEventsService.MuteGames(roomId, TimeSpan.FromMinutes(DEFAULT_MUTE_GAMES_MINUTES));
                    return true;
                }

                if (actionValue.Equals("unmute", StringComparison.OrdinalIgnoreCase) ||
                    actionValue.Equals("active", StringComparison.OrdinalIgnoreCase) ||
                    actionValue.Equals("enabled", StringComparison.OrdinalIgnoreCase))
                {
                    _arcadeEventsService.UnmuteGames(roomId);
                    return true;
                }
            }
        }

        return false;
    }

    private static async Task TryCancelActiveGameAsync(IRoom room)
    {
        if (room.Game == null)
        {
            return;
        }

        try
        {
            if (room.Game is ICancellableGame cancellableGame)
            {
                await cancellableGame.CancelAsync();
            }
            else
            {
                // TODO: ça pue la fuite mémoire
                room.Game = null;
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to cancel active game in room {0}", room.RoomId);
            room.Game = null;
        }
    }

    private static async Task ResetParametersToDefaultsAsync(
        IRoom room,
        IReadOnlyDictionary<Parameter, IParameterDefinition> roomParameters,
        CancellationToken cancellationToken)
    {
        foreach (var (parameterKey, parameterDefinition) in roomParameters)
        {
            var defaultValue = parameterDefinition.DefaultValue ?? string.Empty;
            await room.SetParameterValueAsync(parameterKey, defaultValue, cancellationToken);
        }
    }
}