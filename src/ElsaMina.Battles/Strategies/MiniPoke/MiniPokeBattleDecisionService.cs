using ElsaMina.Logging;
using ElsaMina.Battles.Strategies.MiniPokeSearch;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// Plays singles (gen9 random battles) with a minipoke network exported to ONNX: one instance per model version,
/// each with its own model and encoder (see BattlesModule). It plays the most likely legal action, unless
/// <see cref="MiniPokeActionGuard"/> vetoes it (a move that already had no effect...): then the most likely action
/// left. A model's search variant (<see cref="MiniPokeModelOptions.Search"/>) searches one turn ahead on Showdown's
/// simulator among the actions the guard allows and plays what the search finds best (<see cref="MiniPokeTurnSearch"/>).
/// Falls back to the damage-calc strategy when the model isn't loaded, the state can't
/// be encoded (doubles, team preview) or the chosen action can't be mapped.
/// </summary>
public class MiniPokeBattleDecisionService : IBattleDecisionService
{
    private const int FirstMoveAction = 6;
    private const int FirstTeraAction = 10;

    private readonly IMiniPokeModel _model;
    private readonly IMiniPokeEncoder _encoder;
    private readonly CalcBasedBattleDecisionService _fallback;
    private readonly MiniPokeActionGuard _guard;
    private readonly MiniPokeTurnSearch _search;
    private readonly MiniPokeModelOptions _options;
    private readonly MiniPokeValueCalibration _calibration;

    public MiniPokeBattleDecisionService(IMiniPokeModel model,
        IMiniPokeEncoder encoder,
        CalcBasedBattleDecisionService fallback,
        MiniPokeActionGuard guard,
        MiniPokeTurnSearch search,
        MiniPokeModelOptions options)
    {
        _model = model;
        _encoder = encoder;
        _fallback = fallback;
        _guard = guard;
        _search = search;
        _options = options;
        _calibration = MiniPokeValueCalibration.Load(options.ValueCalibrationPath);
    }

    /// <summary>
    /// The strategy this model version plays as.
    /// </summary>
    public BattleDecisionStrategy Strategy => _options.Strategy;

    /// <summary>
    /// Whether it searches: a search variant, with Showdown's simulator available.
    /// </summary>
    public bool CanSearch => _options.Search && _search.IsAvailable;

    public async Task<BattleDecision> GetDecisionAsync(BattleContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.IsBattleOver || context.Wait)
        {
            return null;
        }

        if (!_model.IsLoaded || context.TeamPreview || context.ActiveSlots.Count > 1)
        {
            return await _fallback.GetDecisionAsync(context, cancellationToken);
        }

        BattleObservation observation;
        float[] logits;
        float value;
        try
        {
            observation = _encoder.Encode(context);
            if (observation == null || !observation.Mask.Any(legal => legal))
            {
                return await _fallback.GetDecisionAsync(context, cancellationToken);
            }

            (logits, value) = _model.Evaluate(observation);
        }
        catch (Exception exception)
        {
            // Never leave a battle without a choice: the timer would forfeit it
            Log.Warning(exception, "{Strategy} failed in {Room}; using fallback", Strategy, context.RoomId);
            return await _fallback.GetDecisionAsync(context, cancellationToken);
        }

        var allowed = Allowed(ArgMax(logits, observation.Mask), observation, context);
        var action = ArgMax(logits, allowed);
        action = await SearchAsync(action, logits, allowed, observation, context, cancellationToken);
        var decision = ToDecision(action, observation);

        if (decision == null)
        {
            Log.Warning("{Strategy} action {Action} could not be mapped in {Room}; using fallback", Strategy, action,
                context.RoomId);
            return await _fallback.GetDecisionAsync(context, cancellationToken);
        }

        Log.Debug("{Strategy} decision in {Room}: action {Action}, value {Value:F2} (expected return {Expected:F2})",
            Strategy, context.RoomId, action, value, _calibration.Apply(value));
        return decision;
    }

    /// <summary>
    /// The legal actions the guard doesn't veto (all the legal ones when it would veto them all, or fails).
    /// <paramref name="action"/>, the model's choice, is logged when it is vetoed.
    /// </summary>
    private bool[] Allowed(int action, BattleObservation observation, BattleContext context)
    {
        IReadOnlyDictionary<int, string> vetoes;
        try
        {
            vetoes = _guard.GetVetoes(context, observation);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "{Strategy} guard failed in {Room}; playing the model's action", Strategy,
                context.RoomId);
            return observation.Mask;
        }

        var allowed = observation.Mask.Select((legal, index) => legal && !vetoes.ContainsKey(index)).ToArray();
        if (!allowed.Any(legal => legal))
        {
            return observation.Mask;
        }

        if (action >= 0 && vetoes.TryGetValue(action, out var reason))
        {
            Log.Information("{Strategy} guard in {Room}: action {Action} vetoed ({Reason})", Strategy,
                context.RoomId, action, reason);
        }

        return allowed;
    }

    /// <summary>
    /// The action the one-turn search finds best among the allowed ones, or <paramref name="action"/> without a
    /// simulator, when there is nothing to search, or when the search fails or runs out of time.
    /// </summary>
    private async Task<int> SearchAsync(int action, float[] logits, bool[] allowed, BattleObservation observation,
        BattleContext context, CancellationToken cancellationToken)
    {
        if (action < 0 || !CanSearch)
        {
            return action;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_search.Timeout);
            var searched = await _search.ChooseAsync(context, logits, allowed, observation, _model, _encoder,
                timeout.Token);
            if (searched is { } found && found != action)
            {
                Log.Debug("{Strategy} search in {Room}: action {Action} -> {Searched}", Strategy, context.RoomId,
                    action, found);
                return found;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException ||
                                          !cancellationToken.IsCancellationRequested)
        {
            Log.Warning(exception, "{Strategy} search failed in {Room}; playing the model's action", Strategy,
                context.RoomId);
        }

        return action;
    }

    private static BattleDecision ToDecision(int action, BattleObservation observation)
    {
        if (action < 0)
        {
            return null;
        }

        if (action < FirstMoveAction)
        {
            // Model slot -> current request order (Showdown reorders the team after each switch).
            var sideIndex = observation.TeamSlotToSideIndex[action];
            return sideIndex < 0 ? null : new BattleDecision(BattleDecisionType.Switch, [sideIndex + 1]);
        }

        // Model move -> position in the request's moves, which can differ (locked moves, Struggle, recharge).
        var moveIndex = (action - FirstMoveAction) % BattleObservation.MoveCount;
        var requestIndex = observation.MoveSlotToRequestIndex[moveIndex];
        var terastallize = action >= FirstTeraAction;
        return requestIndex < 0
            ? null
            : new BattleDecision(BattleDecisionType.Move, [requestIndex + 1], terastallize);
    }

    private static int ArgMax(float[] logits, bool[] mask)
    {
        var best = -1;
        for (var i = 0; i < logits.Length; i++)
        {
            if (mask[i] && (best < 0 || logits[i] > logits[best]))
            {
                best = i;
            }
        }

        return best;
    }
}
