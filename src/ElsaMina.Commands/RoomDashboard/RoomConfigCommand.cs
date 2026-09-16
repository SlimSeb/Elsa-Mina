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

        await ApplyConfigurationPairsAsync(context, room, roomId, parts, cancellationToken);
    }

    private async Task ApplyConfigurationPairsAsync(IContext context, IRoom room, string roomId, string[] parts,
        CancellationToken cancellationToken)
    {
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

                if (!await TryApplyParameterPairAsync(context, room, pair, roomParameters, cancellationToken))
                {
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

    private static async Task<bool> TryApplyParameterPairAsync(
        IContext context,
        IRoom room,
        string pair,
        IReadOnlyDictionary<Parameter, IParameterDefinition> roomParameters,
        CancellationToken cancellationToken)
    {
        var items = pair.Split('=');
        if (items.Length != 2)
        {
            context.ReplyLocalizedMessage("room_config_invalid_pair", pair);
            return false;
        }

        var parameterId = items[0].Trim();
        var value = items[1].Trim();
        var match = FindParameter(roomParameters, parameterId);
        if (match.Value == null)
        {
            context.ReplyLocalizedMessage("room_config_unknown_parameter", parameterId);
            return false;
        }

        if (match.Value.Type == RoomBotConfigurationType.Boolean &&
            value.Equals("toggle", StringComparison.OrdinalIgnoreCase))
        {
            var currentValue = await room.GetParameterValueAsync(match.Key, cancellationToken);
            value = (!currentValue.ToBoolean()).ToString().ToLowerInvariant();
        }

        if (await room.SetParameterValueAsync(match.Key, value, cancellationToken))
        {
            return true;
        }

        context.ReplyLocalizedMessage("room_config_invalid_value", value, parameterId);
        return false;
    }

    private static KeyValuePair<Parameter, IParameterDefinition> FindParameter(
        IReadOnlyDictionary<Parameter, IParameterDefinition> roomParameters,
        string parameterId)
    {
        return roomParameters.FirstOrDefault(kvp =>
            string.Equals(kvp.Value.Identifier, parameterId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kvp.Key.ToString(), parameterId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> TryHandleQuickActionAsync(
        string pair,
        IRoom room,
        string roomId,
        IReadOnlyDictionary<Parameter, IParameterDefinition> roomParameters,
        CancellationToken cancellationToken)
    {
        if (await TryHandleNamedActionAsync(pair, room, roomId, roomParameters, cancellationToken))
        {
            return true;
        }

        if (!pair.Contains('='))
        {
            return false;
        }

        var split = pair.Split('=', 2);
        var actionKey = split[0].Trim();
        var actionValue = split[1].Trim();

        if (IsAnyOf(actionKey, "toggle") &&
            await TryToggleBooleanParameterAsync(room, actionValue, roomParameters, cancellationToken))
        {
            return true;
        }

        if (IsAnyOf(actionKey, "action"))
        {
            return await TryHandleNamedActionAsync(actionValue, room, roomId, roomParameters, cancellationToken);
        }

        if (IsAnyOf(actionKey, "mutegames"))
        {
            return HandleMuteGamesValue(roomId, actionValue);
        }

        if (IsAnyOf(actionKey, "games"))
        {
            return TryHandleGamesStateValue(roomId, actionValue);
        }

        return false;
    }

    private async Task<bool> TryHandleNamedActionAsync(
        string action,
        IRoom room,
        string roomId,
        IReadOnlyDictionary<Parameter, IParameterDefinition> roomParameters,
        CancellationToken cancellationToken)
    {
        if (IsAnyOf(action, "mutegames", "mute"))
        {
            _arcadeEventsService.MuteGames(roomId, TimeSpan.FromMinutes(DEFAULT_MUTE_GAMES_MINUTES));
            return true;
        }

        if (IsAnyOf(action, "unmutegames", "unmute"))
        {
            _arcadeEventsService.UnmuteGames(roomId);
            return true;
        }

        if (IsAnyOf(action, "togglegames"))
        {
            ToggleGamesMuteState(roomId);
            return true;
        }

        if (IsAnyOf(action, "cancelgame", "endgame"))
        {
            await TryCancelActiveGameAsync(room);
            return true;
        }

        if (IsAnyOf(action, "cancelbets", "clearbets", "returnbets"))
        {
            await _tournamentBettingService.ReturnBetsAsync(roomId, cancellationToken);
            return true;
        }

        if (IsAnyOf(action, "reset", "defaults", "resetdefaults"))
        {
            await ResetParametersToDefaultsAsync(room, roomParameters, cancellationToken);
            return true;
        }

        return false;
    }

    private bool HandleMuteGamesValue(string roomId, string actionValue)
    {
        if (int.TryParse(actionValue, out var durationMinutes) && durationMinutes > 0)
        {
            _arcadeEventsService.MuteGames(roomId, TimeSpan.FromMinutes(durationMinutes));
            return true;
        }

        if (IsAnyOf(actionValue, "false", "off"))
        {
            _arcadeEventsService.UnmuteGames(roomId);
            return true;
        }

        _arcadeEventsService.MuteGames(roomId, TimeSpan.FromMinutes(DEFAULT_MUTE_GAMES_MINUTES));
        return true;
    }

    private bool TryHandleGamesStateValue(string roomId, string actionValue)
    {
        if (IsAnyOf(actionValue, "mute", "muted", "disabled"))
        {
            _arcadeEventsService.MuteGames(roomId, TimeSpan.FromMinutes(DEFAULT_MUTE_GAMES_MINUTES));
            return true;
        }

        if (IsAnyOf(actionValue, "unmute", "active", "enabled"))
        {
            _arcadeEventsService.UnmuteGames(roomId);
            return true;
        }

        return false;
    }

    private void ToggleGamesMuteState(string roomId)
    {
        if (_arcadeEventsService.AreGamesMuted(roomId))
        {
            _arcadeEventsService.UnmuteGames(roomId);
            return;
        }

        _arcadeEventsService.MuteGames(roomId, TimeSpan.FromMinutes(DEFAULT_MUTE_GAMES_MINUTES));
    }

    private static async Task<bool> TryToggleBooleanParameterAsync(
        IRoom room,
        string parameterId,
        IReadOnlyDictionary<Parameter, IParameterDefinition> roomParameters,
        CancellationToken cancellationToken)
    {
        var match = FindParameter(roomParameters, parameterId);
        if (match.Value == null || match.Value.Type != RoomBotConfigurationType.Boolean)
        {
            return false;
        }

        var currentValue = await room.GetParameterValueAsync(match.Key, cancellationToken);
        var toggledValue = (!currentValue.ToBoolean()).ToString().ToLowerInvariant();
        await room.SetParameterValueAsync(match.Key, toggledValue, cancellationToken);
        return true;
    }

    private static bool IsAnyOf(string value, params string[] candidates)
    {
        return candidates.Any(candidate => value.Equals(candidate, StringComparison.OrdinalIgnoreCase));
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