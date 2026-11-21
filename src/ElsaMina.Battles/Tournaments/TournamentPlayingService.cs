using System.Text.Json;
using ElsaMina.Core;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Tournaments;

/// <summary>
/// Plays the bot's tournaments: joins those in a format it plays (when asked, or on its own in the rooms that enable
/// <see cref="TournamentsRoomParameters.AutoJoinTournaments"/>), then gets each of its matches started. The battles
/// are played by <see cref="BattleService"/> like any other: the server puts the bot in their room.
/// <para>
/// Showdown matches tournament players in one of two ways, depending on its version:
/// <list type="bullet">
/// <item>since October 2026, each player readies for their next match (/tour ready), and the battle starts once both
/// are ready. Until it is ready, a player is told its next opponent in "challenged" ("(next available player)" when
/// not known yet), then in "challenging";</item>
/// <item>before, a player challenges one of the opponents listed in "challenges" (/tour challenge USER), who is told
/// in "challenged" and accepts (/tour acceptchallenge).</item>
/// </list>
/// /tour acceptchallenge is an alias of /tour ready on the newer servers: being "challenged" is answered with it on
/// both.
/// </para>
/// <para>
/// Readying or challenging validates the bot's team, which Showdown counts against the same limit as ladder searches
/// (see <see cref="LadderingService"/>), and a refused one only gets a popup: the command is sent again while the
/// server still waits for it, after <see cref="FIRST_RETRY_DELAY"/> and longer each time, and at once when the server
/// warns the bot it is about to be disqualified.
/// </para>
/// Showdown sends a tournament's |update|s right away but its other messages (|create|, |start|, |join|) with the
/// room's next batch, so after the updates they follow: everything here is read from the updates, which each update
/// period ends with an |updateEnd|.
/// </summary>
public sealed class TournamentPlayingService : ITournamentPlayingService, IDisposable
{
    public const string JOIN_COMMAND = "/tour join";
    public const string LEAVE_COMMAND = "/tour leave";
    public const string ACCEPT_COMMAND = "/tour acceptchallenge";
    public const string CHALLENGE_COMMAND = "/tour challenge";

    public static readonly TimeSpan FIRST_RETRY_DELAY = TimeSpan.FromSeconds(30);

    // Showdown's validation limit lasts 3 minutes at most, waiting longer brings nothing
    public static readonly TimeSpan MAX_RETRY_DELAY = TimeSpan.FromMinutes(3);

    /// <summary>
    /// The formats whose battles the bot plays in tournaments: random battles, which need no team, in the generation
    /// its strategies are made for.
    /// </summary>
    public static readonly IReadOnlySet<string> SUPPORTED_FORMAT_IDS = new HashSet<string> { "gen9randombattle" };

    private readonly IBot _bot;
    private readonly IBattleTeamSender _teamSender;
    private readonly IRoomsManager _roomsManager;
    private readonly IConfiguration _configuration;
    private readonly ISystemService _systemService;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, TournamentState> _tournaments = new();

    public TournamentPlayingService(IBot bot, IBattleTeamSender teamSender, IRoomsManager roomsManager,
        IConfiguration configuration, ISystemService systemService)
    {
        _bot = bot;
        _teamSender = teamSender;
        _roomsManager = roomsManager;
        _configuration = configuration;
        _systemService = systemService;
    }

    public async Task HandleMessageAsync(string[] parts, string roomId, CancellationToken cancellationToken = default)
    {
        if (parts.Length < 2 || string.IsNullOrEmpty(roomId))
        {
            return;
        }

        // The bot joined or left the room: what comes next (or nothing) describes its tournament
        if (parts[1] is "init" or "deinit")
        {
            Remove(roomId);
            return;
        }

        if (parts.Length < 3 || parts[1] != "tournament")
        {
            return;
        }

        switch (parts[2])
        {
            case "create" when parts.Length > 3:
                OnCreated(roomId, parts[3]);
                await TryAutoJoinAsync(roomId, cancellationToken);
                break;
            case "update" when parts.Length > 3:
                OnUpdated(roomId, string.Join("|", parts[3..]));
                break;
            case "updateEnd":
                await TryAutoJoinAsync(roomId, cancellationToken);
                PlayNextMatch(roomId, isUrgent: false);
                break;
            case "battlestart" when parts.Length > 4:
                OnBattleStarted(roomId, parts);
                break;
            // |tournament|autodq|target|TIME: the bot is disqualified in TIME ms unless its match starts
            case "autodq" when parts.Length > 3 && parts[3] == "target":
                PlayNextMatch(roomId, isUrgent: true);
                break;
            case "end":
            case "forceend":
                Remove(roomId);
                break;
            case "error":
                Log.Warning("Tournament error in {RoomId}: {Error}", roomId, string.Join("|", parts[3..]));
                break;
        }
    }

    public TournamentJoinResult Join(string roomId)
    {
        lock (_lock)
        {
            if (!_tournaments.TryGetValue(roomId, out var tournament))
            {
                return TournamentJoinResult.NoTournament;
            }

            if (tournament.IsJoined)
            {
                return TournamentJoinResult.AlreadyJoined;
            }

            if (tournament.IsStarted)
            {
                return TournamentJoinResult.AlreadyStarted;
            }

            if (!IsSupported(tournament.FormatId))
            {
                return TournamentJoinResult.UnsupportedFormat;
            }

            RequestJoin(roomId, tournament);
            return TournamentJoinResult.Joining;
        }
    }

    public bool Leave(string roomId)
    {
        lock (_lock)
        {
            if (!_tournaments.TryGetValue(roomId, out var tournament) || !tournament.IsJoined)
            {
                return false;
            }

            ClearNextMatch(tournament);
            _bot.Say(roomId, LEAVE_COMMAND);
            Log.Information("Leaving the tournament of {RoomId}", roomId);
            return true;
        }
    }

    private static bool IsSupported(string formatId) =>
        !string.IsNullOrEmpty(formatId) && SUPPORTED_FORMAT_IDS.Contains(formatId);

    private void OnCreated(string roomId, string formatId)
    {
        lock (_lock)
        {
            GetOrAdd(roomId).FormatId = formatId.ToLowerAlphaNum();
        }
    }

    private void OnUpdated(string roomId, string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            Log.Error(exception, "Could not read the tournament update of {RoomId}: {Update}", roomId, json);
            return;
        }

        using (document)
        {
            var update = document.RootElement;
            if (update.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            lock (_lock)
            {
                ApplyUpdate(GetOrAdd(roomId), update);
            }
        }
    }

    /// <summary>
    /// An update only has what changed: a property it does not have keeps its value, a null one is cleared.
    /// </summary>
    private static void ApplyUpdate(TournamentState tournament, JsonElement update)
    {
        // "format" is the tournament's name, its format ("[Gen 9] Random Battle") unless named otherwise, in which
        // case the format comes in "teambuilderFormat"
        if (update.TryGetProperty("teambuilderFormat", out var teambuilderFormat) &&
            teambuilderFormat.ValueKind == JsonValueKind.String)
        {
            tournament.FormatId = teambuilderFormat.GetString().ToLowerAlphaNum();
        }
        else if (tournament.FormatId == null && update.TryGetProperty("format", out var format) &&
                 format.ValueKind == JsonValueKind.String)
        {
            tournament.FormatId = format.GetString().ToLowerAlphaNum();
        }

        if (TryGetBoolean(update, "isStarted", out var isStarted))
        {
            tournament.IsStarted = isStarted;
        }

        if (TryGetBoolean(update, "isJoined", out var isJoined))
        {
            tournament.IsJoined = isJoined;
            if (!isJoined)
            {
                ClearNextMatch(tournament);
            }
        }

        if (update.TryGetProperty("challenges", out var challenges))
        {
            tournament.Challenges = challenges.ValueKind == JsonValueKind.Array
                ? challenges.EnumerateArray()
                    .Where(name => name.ValueKind == JsonValueKind.String)
                    .Select(name => name.GetString())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList()
                : [];
        }

        if (update.TryGetProperty("challenged", out var challenged))
        {
            tournament.Challenged = ReadName(challenged);
        }

        if (update.TryGetProperty("challenging", out var challenging))
        {
            tournament.Challenging = ReadName(challenging);
        }
    }

    private static bool TryGetBoolean(JsonElement update, string propertyName, out bool value)
    {
        value = false;
        if (!update.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static string ReadName(JsonElement name)
    {
        var value = name.ValueKind == JsonValueKind.String ? name.GetString() : null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private void OnBattleStarted(string roomId, string[] parts)
    {
        // |tournament|battlestart|USER1|USER2|ROOMID
        var bot = _configuration.Name.ToLowerAlphaNum();
        if (parts[3].ToLowerAlphaNum() != bot && parts[4].ToLowerAlphaNum() != bot)
        {
            return;
        }

        lock (_lock)
        {
            if (_tournaments.TryGetValue(roomId, out var tournament))
            {
                ClearNextMatch(tournament);
            }
        }

        Log.Information("Tournament battle of {RoomId} started in {BattleRoomId}", roomId,
            parts.Length > 5 ? parts[5] : string.Empty);
    }

    private async Task TryAutoJoinAsync(string roomId, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_tournaments.TryGetValue(roomId, out var tournament) || !CanAutoJoin(tournament))
            {
                return;
            }
        }

        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            return;
        }

        var isEnabled = (await room.GetParameterValueAsync(TournamentsRoomParameters.AutoJoinTournaments,
            cancellationToken)).ToBoolean();
        if (!isEnabled)
        {
            return;
        }

        lock (_lock)
        {
            // A command may have joined it in the meantime
            if (_tournaments.TryGetValue(roomId, out var tournament) && CanAutoJoin(tournament))
            {
                RequestJoin(roomId, tournament);
            }
        }
    }

    private static bool CanAutoJoin(TournamentState tournament) =>
        !tournament.HasRequestedJoin && !tournament.IsStarted && !tournament.IsJoined &&
        IsSupported(tournament.FormatId);

    private void RequestJoin(string roomId, TournamentState tournament)
    {
        tournament.HasRequestedJoin = true;
        _bot.Say(roomId, JOIN_COMMAND);
        Log.Information("Joining the {Format} tournament of {RoomId}", tournament.FormatId, roomId);
    }

    /// <summary>
    /// Sends what the server waits for to start the bot's next match, if anything. A command already sent is not sent
    /// again before its retry, unless <paramref name="isUrgent"/>.
    /// </summary>
    private void PlayNextMatch(string roomId, bool isUrgent)
    {
        lock (_lock)
        {
            if (!_tournaments.TryGetValue(roomId, out var tournament))
            {
                return;
            }

            var action = GetNextMatchAction(tournament);
            if (action == null)
            {
                // Ready or challenging: the server took the command. Otherwise the match can come back (a challenge
                // refused by older servers lists the opponent again once its validation fails): the retry waits for it
                if (tournament.Challenging != null)
                {
                    ClearSentAction(tournament);
                }

                return;
            }

            if (action == tournament.SentAction && !isUrgent)
            {
                return;
            }

            if (action != tournament.SentAction)
            {
                tournament.UnansweredCount = 0;
            }

            SendAction(roomId, tournament, action);
        }
    }

    private static string GetNextMatchAction(TournamentState tournament)
    {
        // The server tells the bot about its matches in any tournament it plays, even one it was put in
        // (/tour replace): it only starts those it can play
        if (!IsSupported(tournament.FormatId))
        {
            return null;
        }

        if (tournament.Challenged != null)
        {
            return ACCEPT_COMMAND;
        }

        return tournament.Challenging == null && tournament.Challenges.Count > 0
            ? $"{CHALLENGE_COMMAND} {tournament.Challenges[0]}"
            : null;
    }

    private void SendAction(string roomId, TournamentState tournament, string action)
    {
        tournament.SentAction = action;
        // Readying validates the bot's team: the formats it plays generate theirs
        _teamSender.SendWithTeam(null, action, roomId);
        ScheduleRetry(roomId, tournament, GetRetryDelay(tournament.UnansweredCount));
        Log.Information("Sent {Action} for the next match of the {RoomId} tournament", action, roomId);
    }

    // 30s, 1min, 2min, then 3min
    private static TimeSpan GetRetryDelay(int unansweredCount)
    {
        var delay = FIRST_RETRY_DELAY;
        for (var i = 0; i < unansweredCount && delay < MAX_RETRY_DELAY; i++)
        {
            delay *= 2;
        }

        return delay < MAX_RETRY_DELAY ? delay : MAX_RETRY_DELAY;
    }

    private void ScheduleRetry(string roomId, TournamentState tournament, TimeSpan delay)
    {
        CancelRetry(tournament);
        var retry = new CancellationTokenSource();
        tournament.Retry = retry;
        _ = RetryAsync(roomId, tournament, delay, retry);
    }

    private async Task RetryAsync(string roomId, TournamentState tournament, TimeSpan delay,
        CancellationTokenSource retry)
    {
        try
        {
            await _systemService.SleepAsync(delay, retry.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_lock)
        {
            // Answered, replaced or forgotten while waiting
            if (tournament.Retry != retry)
            {
                return;
            }

            tournament.Retry = null;
            retry.Dispose();
            var action = GetNextMatchAction(tournament);
            if (action == null)
            {
                ClearSentAction(tournament);
                return;
            }

            tournament.UnansweredCount++;
            Log.Warning("No answer from the server to {Action} in {RoomId} (attempt {Attempt}), sending it again",
                tournament.SentAction, roomId, tournament.UnansweredCount);
            SendAction(roomId, tournament, action);
        }
    }

    private static void ClearNextMatch(TournamentState tournament)
    {
        tournament.Challenged = null;
        tournament.Challenging = null;
        tournament.Challenges = [];
        ClearSentAction(tournament);
    }

    private static void ClearSentAction(TournamentState tournament)
    {
        tournament.SentAction = null;
        tournament.UnansweredCount = 0;
        CancelRetry(tournament);
    }

    private static void CancelRetry(TournamentState tournament)
    {
        if (tournament.Retry == null)
        {
            return;
        }

        tournament.Retry.Cancel();
        tournament.Retry.Dispose();
        tournament.Retry = null;
    }

    private TournamentState GetOrAdd(string roomId)
    {
        if (!_tournaments.TryGetValue(roomId, out var tournament))
        {
            tournament = new TournamentState();
            _tournaments[roomId] = tournament;
        }

        return tournament;
    }

    private void Remove(string roomId)
    {
        lock (_lock)
        {
            if (_tournaments.Remove(roomId, out var tournament))
            {
                CancelRetry(tournament);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var tournament in _tournaments.Values)
            {
                CancelRetry(tournament);
            }

            _tournaments.Clear();
        }
    }
}
