using ElsaMina.Logging;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// Plays singles (gen9 random battles) with a minipoke network exported to ONNX: one instance per model version,
/// each with its own model and encoder (see BattlesModule). It plays the most likely legal action, unless
/// <see cref="MiniPokeActionGuard"/> vetoes it (a move that already had no effect...): then the most likely action
/// left.
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
    private readonly MiniPokeModelOptions _options;
    private readonly MiniPokeValueCalibration _calibration;

    public MiniPokeBattleDecisionService(IMiniPokeModel model,
        IMiniPokeEncoder encoder,
        CalcBasedBattleDecisionService fallback,
        MiniPokeActionGuard guard,
        MiniPokeModelOptions options)
    {
        _model = model;
        _encoder = encoder;
        _fallback = fallback;
        _guard = guard;
        _options = options;
        _calibration = MiniPokeValueCalibration.Load(options.ValueCalibrationPath);
    }

    /// <summary>
    /// The strategy this model version plays as.
    /// </summary>
    public BattleDecisionStrategy Strategy => _options.Strategy;

    public Task<BattleDecision> GetDecisionAsync(BattleContext context, CancellationToken cancellationToken = default)
    {
        if (context.IsBattleOver || context.Wait)
        {
            return Task.FromResult<BattleDecision>(null);
        }

        if (!_model.IsLoaded || context.TeamPreview || context.ActiveSlots.Count > 1)
        {
            return _fallback.GetDecisionAsync(context, cancellationToken);
        }

        BattleObservation observation;
        float[] logits;
        float value;
        try
        {
            observation = _encoder.Encode(context);
            if (observation == null || !observation.Mask.Any(legal => legal))
            {
                return _fallback.GetDecisionAsync(context, cancellationToken);
            }

            (logits, value) = _model.Evaluate(observation);
        }
        catch (Exception exception)
        {
            // Never leave a battle without a choice: the timer would forfeit it
            Log.Warning(exception, "{Strategy} failed in {Room}; using fallback", Strategy, context.RoomId);
            return _fallback.GetDecisionAsync(context, cancellationToken);
        }

        var action = Guard(ArgMax(logits, observation.Mask), logits, observation, context);
        var decision = ToDecision(action, observation);

        if (decision == null)
        {
            Log.Warning("{Strategy} action {Action} could not be mapped in {Room}; using fallback", Strategy, action,
                context.RoomId);
            return _fallback.GetDecisionAsync(context, cancellationToken);
        }

        Log.Debug("{Strategy} decision in {Room}: action {Action}, value {Value:F2} (expected return {Expected:F2})",
            Strategy, context.RoomId, action, value, _calibration.Apply(value));
        return Task.FromResult(decision);
    }

    /// <summary>
    /// The model's action, or the best one the guard doesn't veto when it vetoes it.
    /// </summary>
    private int Guard(int action, float[] logits, BattleObservation observation, BattleContext context)
    {
        if (action < 0)
        {
            return action;
        }

        IReadOnlyDictionary<int, string> vetoes;
        try
        {
            vetoes = _guard.GetVetoes(context, observation);
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "{Strategy} guard failed in {Room}; playing the model's action", Strategy,
                context.RoomId);
            return action;
        }

        if (!vetoes.TryGetValue(action, out var reason))
        {
            return action;
        }

        var allowed = observation.Mask.Select((legal, index) => legal && !vetoes.ContainsKey(index)).ToArray();
        var guarded = ArgMax(logits, allowed);
        if (guarded < 0)
        {
            return action;
        }

        Log.Information("{Strategy} guard in {Room}: action {Action} vetoed ({Reason}), playing {Guarded}", Strategy,
            context.RoomId, action, reason, guarded);
        return guarded;
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
