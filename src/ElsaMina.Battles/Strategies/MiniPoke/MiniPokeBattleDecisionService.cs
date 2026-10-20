using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// Plays singles (gen9 random battles) with a minipoke network exported to ONNX: one instance per model version,
/// each with its own model and encoder (see BattlesModule).
/// Falls back to the damage-calc strategy when the model isn't loaded, the state can't
/// be encoded (doubles, team preview) or the chosen action can't be mapped.
/// </summary>
public class MiniPokeBattleDecisionService : IBattleDecisionService
{
    private const int FirstMoveAction = 6;
    private const int FirstTeraAction = 10;

    private readonly IMiniPokeModel _model;
    private readonly IMiniPokeEncoder _encoder;
    private readonly IBattleDecisionService _fallback;
    private readonly IRandomService _randomService;
    private readonly MiniPokeModelOptions _options;

    public MiniPokeBattleDecisionService(IMiniPokeModel model,
        IMiniPokeEncoder encoder,
        CalcBasedBattleDecisionService fallback,
        IRandomService randomService,
        MiniPokeModelOptions options)
    {
        _model = model;
        _encoder = encoder;
        _fallback = fallback;
        _randomService = randomService;
        _options = options;
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

        var action = _options.Sample ? SampleAction(logits, observation.Mask) : ArgMax(logits, observation.Mask);
        var decision = ToDecision(action, observation);

        if (decision == null)
        {
            Log.Warning("{Strategy} action {Action} could not be mapped in {Room}; using fallback", Strategy, action,
                context.RoomId);
            return _fallback.GetDecisionAsync(context, cancellationToken);
        }

        Log.Debug("{Strategy} decision in {Room}: action {Action}, value {Value:F2}", Strategy, context.RoomId, action,
            value);
        return Task.FromResult(decision);
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

    private int SampleAction(float[] logits, bool[] mask)
    {
        var temperature = Math.Max(_options.Temperature, 1e-3f);
        var max = logits.Where((_, i) => mask[i]).Max();
        var weights = logits.Select((logit, i) => mask[i] ? Math.Exp((logit - max) / temperature) : 0.0).ToArray();
        var roll = _randomService.NextDouble() * weights.Sum();
        for (var i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll <= 0 && mask[i])
            {
                return i;
            }
        }

        return ArgMax(logits, mask);
    }
}
