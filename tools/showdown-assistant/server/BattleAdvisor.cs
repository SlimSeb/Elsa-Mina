using System.Collections.Concurrent;
using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.MiniPokeSearch;
using ElsaMina.Battles.Tracking;
using ElsaMina.Logging;

namespace ElsaMina.BattleAssistant;

/// <summary>
/// Follows the battles the extension forwards the way the bot follows its own (BattleService): every line goes through
/// <see cref="BattleMessageParser"/> into the room's <see cref="BattleContext"/>, and each request the player must
/// answer is encoded and scored by the model, then checked by the guard, as MiniPokeBattleDecisionService does.
/// Instead of playing the best action, it describes every legal one: its probability, the guard's veto, the button
/// it stands for. The one-turn search runs on demand, on its own copy of the battle, so it never blocks the room.
/// </summary>
public sealed class BattleAdvisor
{
    private const string BATTLE_ROOM_PREFIX = "battle-";
    private const string INIT_LINE = "|init|battle";
    private const string DEINIT_LINE = "|deinit";
    private const string TRAINED_FORMAT = "gen9randombattle";
    private const string MOVE_CHOICE = "move";
    private const string TERASTALLIZE_CHOICE = " terastallize";
    private const string FAINTED = "fainted";
    private const int MAX_SESSIONS = 32;
    private const int SWITCH_ACTIONS = 6;
    private const int FIRST_TERA_ACTION = 10;

    private readonly ConcurrentDictionary<string, BattleSession> _sessions = new(StringComparer.Ordinal);
    private readonly IBattleMessageParser _parser;
    private readonly IMiniPokeEncoder _encoder;
    private readonly IMiniPokeModel _model;
    private readonly MiniPokeActionGuard _guard;
    private readonly MiniPokeTurnSearch _search;
    private readonly IBattleDex _dex;
    private readonly MiniPokeModelOptions _options;

    public BattleAdvisor(IBattleMessageParser parser,
        IMiniPokeEncoder encoder,
        IMiniPokeModel model,
        MiniPokeActionGuard guard,
        MiniPokeTurnSearch search,
        IBattleDex dex,
        MiniPokeModelOptions options)
    {
        _parser = parser;
        _encoder = encoder;
        _model = model;
        _guard = guard;
        _search = search;
        _dex = dex;
        _options = options;
    }

    public AssistantStatus GetStatus() =>
        new(_options.Strategy.GetName(), _model.IsLoaded, _search.IsAvailable, TRAINED_FORMAT);

    public static bool IsBattleRoom(string roomId) =>
        roomId.StartsWith(BATTLE_ROOM_PREFIX, StringComparison.Ordinal);

    /// <summary>
    /// Applies a room's frames in order. The advice is the last decision they asked for, or
    /// <see cref="AdviceStatus.Unchanged"/> when they asked for none.
    /// </summary>
    public BattleAdvice ApplyFrames(string roomId, IEnumerable<string> frames, bool reset)
    {
        if (reset)
        {
            Forget(roomId);
        }

        var session = GetSession(roomId);
        lock (session.Gate)
        {
            BattleAdvice? advice = null;
            foreach (var line in frames.SelectMany(frame => frame.Split('\n')))
            {
                advice = ApplyLine(session, line) ?? advice;
            }

            return advice ?? Describe(session, AdviceStatus.Unchanged);
        }
    }

    public void Forget(string roomId) => _sessions.TryRemove(roomId, out _);

    /// <summary>
    /// Searches one turn ahead for the request <paramref name="rqid"/>, the one the room's last advice answered.
    /// </summary>
    public async Task<SearchAdvice> SearchAsync(string roomId, int rqid, CancellationToken cancellationToken)
    {
        if (!_search.IsAvailable)
        {
            return new SearchAdvice(roomId, rqid, SearchStatus.Unavailable, null,
                "No simulator: start the server with --ShowdownDirectory");
        }

        if (!_sessions.TryGetValue(roomId, out var session))
        {
            return new SearchAdvice(roomId, rqid, SearchStatus.Stale, null);
        }

        PendingDecision? pending;
        List<string> lines;
        lock (session.Gate)
        {
            pending = session.Pending;
            if (pending == null || pending.Rqid != rqid)
            {
                return new SearchAdvice(roomId, rqid, SearchStatus.Stale, null);
            }

            lines = session.Context.Lines.Take(pending.LineCount).ToList();
        }

        // The room keeps receiving lines (chat, timer) while the search runs: it gets the battle as it was
        var context = Replay(roomId, lines);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_search.Timeout);
        try
        {
            var action = await _search.ChooseAsync(context, pending.Logits, pending.Allowed, pending.Observation,
                _model, _encoder, timeout.Token);
            return action is { } found
                ? new SearchAdvice(roomId, rqid, SearchStatus.Searched, found)
                : new SearchAdvice(roomId, rqid, SearchStatus.Skipped, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SearchAdvice(roomId, rqid, SearchStatus.Failed, null, "The search ran out of time");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Log.Warning(exception, "Search failed in {Room}", roomId);
            return new SearchAdvice(roomId, rqid, SearchStatus.Failed, null, exception.Message);
        }
    }

    private BattleSession GetSession(string roomId)
    {
        var session = _sessions.GetOrAdd(roomId, id => new BattleSession(id));
        session.Touch();
        if (_sessions.Count > MAX_SESSIONS)
        {
            // Rooms closed without a |deinit| (the tab was closed)
            var oldest = _sessions.Values.Where(other => other != session).MinBy(other => other.LastUsed);
            if (oldest != null)
            {
                Forget(oldest.RoomId);
            }
        }

        return session;
    }

    private BattleAdvice? ApplyLine(BattleSession session, string line)
    {
        // Same lines as Bot.HandleReceivedMessageAsync: no blank ones, no ">room" header
        if (string.IsNullOrWhiteSpace(line) || line[0] == '>')
        {
            return null;
        }

        if (line.StartsWith(INIT_LINE, StringComparison.Ordinal))
        {
            session.Reset();
        }
        else if (line == DEINIT_LINE)
        {
            Forget(session.RoomId);
            return Describe(session, AdviceStatus.Closed);
        }
        else
        {
            session.MarkStarted();
        }

        var parts = line.Split('|');
        if (parts.Length < 2)
        {
            return null;
        }

        var context = session.Context;
        context.Lines.Add(line);
        if (!_parser.TryApplyMessage(parts, session.RoomId, context, out var result))
        {
            return null;
        }

        return result.Type switch
        {
            BattleMessageType.BattleEnded => Describe(session, AdviceStatus.Over),
            BattleMessageType.RequestUpdated => Advise(session),
            _ => null
        };
    }

    private BattleAdvice Advise(BattleSession session)
    {
        var context = session.Context;
        session.Pending = null;
        if (context.IsBattleOver)
        {
            return Describe(session, AdviceStatus.Over);
        }

        if (context.Wait)
        {
            return Describe(session, AdviceStatus.Waiting);
        }

        var unsupported = context switch
        {
            { TeamPreview: true } => "Team preview: the model does not choose leads",
            { ActiveSlots.Count: > 1 } => "Doubles: the model only plays singles",
            _ when !_model.IsLoaded => "The model is not loaded: see the server's log",
            _ => null
        };
        if (unsupported != null)
        {
            return Describe(session, AdviceStatus.Unsupported) with { Message = unsupported };
        }

        BattleObservation? observation;
        float[] logits;
        float value;
        try
        {
            observation = _encoder.Encode(context);
            if (observation == null || !observation.Mask.Any(legal => legal))
            {
                return Describe(session, AdviceStatus.Unsupported) with
                {
                    Message = "The model cannot read this position"
                };
            }

            (logits, value) = _model.Evaluate(observation);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "The model failed in {Room}", context.RoomId);
            return Describe(session, AdviceStatus.Unsupported) with
            {
                Message = $"The model failed on this position: {exception.Message}"
            };
        }

        var vetoes = GetVetoes(context, observation);
        var probabilities = Softmax(logits, observation.Mask);
        var actions = Enumerable.Range(0, BattleObservation.ActionCount)
            .Where(action => observation.Mask[action])
            .Select(action => DescribeAction(action, context, observation, probabilities[action],
                vetoes.GetValueOrDefault(action)))
            .OfType<ActionAdvice>()
            .OrderByDescending(action => action.Probability)
            .ToList();

        // The guard's choice among what Showdown's buttons can play; all of them when it would veto them all
        var allowed = new bool[BattleObservation.ActionCount];
        var candidates = actions.Any(action => action.Veto == null)
            ? actions.Where(action => action.Veto == null).ToList()
            : actions;
        foreach (var action in candidates)
        {
            allowed[action.Action] = true;
        }

        int? best = actions.Count == 0 ? null : candidates.MaxBy(action => logits[action.Action])!.Action;
        session.Pending = new PendingDecision(context.Rqid, context.Lines.Count, observation, logits, allowed);
        return Describe(session, AdviceStatus.Advice) with { Value = value, Best = best, Actions = actions };
    }

    private BattleAdvice Describe(BattleSession session, AdviceStatus status)
    {
        var context = session.Context;
        return new BattleAdvice
        {
            RoomId = session.RoomId,
            Status = status,
            Rqid = context.Rqid,
            Turn = context.Tracked.Turn,
            Format = context.Format,
            IsTrainedFormat = context.Format == TRAINED_FORMAT,
            Partial = session.IsPartial,
            CanSearch = _search.IsAvailable
        };
    }

    private IReadOnlyDictionary<int, string> GetVetoes(BattleContext context, BattleObservation observation)
    {
        try
        {
            return _guard.GetVetoes(context, observation);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "The guard failed in {Room}", context.RoomId);
            return new Dictionary<int, string>();
        }
    }

    /// <summary>
    /// The action as the request's buttons offer it, null when it maps to none (the bot then falls back on the
    /// damage calc).
    /// </summary>
    private ActionAdvice? DescribeAction(int action, BattleContext context, BattleObservation observation,
        double probability, string? veto)
    {
        if (action < SWITCH_ACTIONS)
        {
            // Model slot -> current request order, which Showdown changes after each switch
            var sideIndex = observation.TeamSlotToSideIndex[action];
            if (sideIndex < 0 || sideIndex >= context.SidePokemon.Count)
            {
                return null;
            }

            var pokemon = context.SidePokemon[sideIndex];
            return new ActionAdvice(action, ActionKind.Switch, GetName(pokemon), GetHealth(pokemon), null, false,
                null, $"switch {sideIndex + 1}", probability, veto);
        }

        // Model move -> position in the request's moves (they differ when locked into a move, or recharging)
        var requestIndex = observation.MoveSlotToRequestIndex[(action - SWITCH_ACTIONS) % BattleObservation.MoveCount];
        var slot = context.ActiveSlots.FirstOrDefault();
        if (slot == null || requestIndex < 0 || requestIndex >= slot.Moves.Count)
        {
            return null;
        }

        var move = slot.Moves[requestIndex];
        var terastallize = action >= FIRST_TERA_ACTION;
        var type = _dex.TryGetMove(move.Id, out var moveData) ? moveData.Type : null;
        var teraType = terastallize ? GetTeraType(context, slot) : null;
        var detail = move.MaxPp > 0 ? $"{move.Pp}/{move.MaxPp} PP" : "";
        var choice = $"{MOVE_CHOICE} {requestIndex + 1}{(terastallize ? TERASTALLIZE_CHOICE : "")}";
        return new ActionAdvice(action, ActionKind.Move, move.Name, detail, type, terastallize, teraType, choice,
            probability, veto);
    }

    private static string GetName(BattlePokemonState pokemon)
    {
        // "p1: Garchomp": the name its switch button shows
        var separator = pokemon.Ident.IndexOf(": ", StringComparison.Ordinal);
        return separator >= 0 ? pokemon.Ident[(separator + 2)..] : pokemon.Details.Split(',')[0];
    }

    private static string GetHealth(BattlePokemonState pokemon)
    {
        if (pokemon.IsFainted)
        {
            return FAINTED;
        }

        var health = pokemon.MaxHp > 0
            ? $"{Math.Round(100.0 * pokemon.CurrentHp / pokemon.MaxHp)}%"
            : "";
        // "250/301 par": the status after the HP
        var status = pokemon.Condition.Split(' ').Skip(1).FirstOrDefault();
        return string.IsNullOrEmpty(status) ? health : $"{health} {status.ToUpperInvariant()}";
    }

    private static string? GetTeraType(BattleContext context, BattleActiveSlot slot)
    {
        if (!string.IsNullOrEmpty(slot.CanTerastallize))
        {
            return slot.CanTerastallize;
        }

        return context.SidePokemon.FirstOrDefault(pokemon => pokemon.IsActive)?.TeraType;
    }

    private static double[] Softmax(float[] logits, bool[] mask)
    {
        var probabilities = new double[logits.Length];
        var max = double.NegativeInfinity;
        for (var i = 0; i < logits.Length; i++)
        {
            if (mask[i])
            {
                max = Math.Max(max, logits[i]);
            }
        }

        var sum = 0.0;
        for (var i = 0; i < logits.Length; i++)
        {
            probabilities[i] = mask[i] ? Math.Exp(logits[i] - max) : 0;
            sum += probabilities[i];
        }

        for (var i = 0; i < logits.Length && sum > 0; i++)
        {
            probabilities[i] /= sum;
        }

        return probabilities;
    }

    private BattleContext Replay(string roomId, List<string> lines)
    {
        var context = new BattleContext(roomId);
        foreach (var line in lines)
        {
            context.Lines.Add(line);
            _parser.TryApplyMessage(line.Split('|'), roomId, context, out _);
        }

        return context;
    }
}
