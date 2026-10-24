using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Tracking;

namespace ElsaMina.Battles.Strategies.MiniPokeV1;

/// <summary>
/// The features of encoder.py as minipoke v1 was trained with them (_mon_features, _move_features and the field
/// block of encode). Must match it value for value: MiniPokeV1ParityTest checks it against battles recorded
/// with that encoder.py.
/// </summary>
public class MiniPokeV1Encoder : MiniPokeEncoder
{
    // Scalar features around the named blocks, as counted in encoder.py's F_MON, F_MOVE and F_FIELD
    private const int MON_LEADING_FEATURES = 6;
    private const int MON_TERASTALLIZED_FEATURES = 1;
    private const int MON_LEVEL_FEATURES = 1;
    private const int MON_KNOWN_ITEM_ABILITY_FEATURES = 2;
    private const int MON_MOVE_STATE_FEATURES = 3;
    private const int MON_KNOWN_MOVES_FEATURES = 1;
    private const int MOVE_LEADING_FEATURES = 5;
    private const int MOVE_EFFECTIVENESS_FEATURES = 3;
    private const int MOVE_EFFECT_FEATURES = 8;
    private const int FIELD_TRAILING_FEATURES = 9;

    private const int MAX_BASE_POWER = 250;
    private const double BASE_POWER_SCALE = 150.0;
    private const int MIN_PRIORITY = -7;
    private const int MAX_PRIORITY = 5;
    private const double BOOST_SCALE = 6.0;
    private const double BASE_STAT_SCALE = 200.0;
    private const double LEVEL_SCALE = 100.0;
    private const int MAX_TURN = 50;
    private const double TEAM_SIZE = 6.0;
    private const double MAX_EFFECTIVENESS_LOG = 2.0;
    private const string STATUS_CATEGORY = "STATUS";

    public MiniPokeV1Encoder(MiniPokeModelOptions options, IBattleDex dex) : base(options, dex)
    {
        if (Spec == null)
        {
            return;
        }

        var orderings = Spec.Orderings;
        var typeCount = orderings.Types.Count;
        RequireFeatureSizes(
            MON_LEADING_FEATURES + orderings.Statuses.Count + orderings.Boosts.Count + typeCount + typeCount +
            MON_TERASTALLIZED_FEATURES + orderings.Stats.Count + MON_LEVEL_FEATURES +
            MON_KNOWN_ITEM_ABILITY_FEATURES + orderings.Effects.Count + MON_MOVE_STATE_FEATURES +
            MON_KNOWN_MOVES_FEATURES,
            MOVE_LEADING_FEATURES + orderings.Categories.Count + typeCount + MOVE_EFFECTIVENESS_FEATURES +
            MOVE_EFFECT_FEATURES,
            orderings.Weathers.Count + orderings.Fields.Count + 2 * orderings.SideConditions.Count +
            FIELD_TRAILING_FEATURES);
    }

    protected override void WriteMonFeatures(float[] features, int start, TrackedPokemon pokemon, bool isMine)
    {
        var orderings = Spec.Orderings;
        if (pokemon == null)
        {
            features[start + 1] = 1f;
            features[start + 2] = ToFloat(isMine);
            return;
        }

        features[start] = 1f;
        features[start + 2] = ToFloat(isMine);
        features[start + 3] = ToFloat(pokemon.Active);
        features[start + 4] = ToFloat(pokemon.Fainted);
        features[start + 5] = (float)pokemon.CurrentHpFraction;
        var offset = start + 6;

        SetOneHot(features, offset, orderings.Statuses, pokemon.Status);
        offset += orderings.Statuses.Count;

        for (var i = 0; i < orderings.Boosts.Count; i++)
        {
            features[offset + i] = (float)(pokemon.Boosts.GetValueOrDefault(orderings.Boosts[i]) / BOOST_SCALE);
        }

        offset += orderings.Boosts.Count;

        foreach (var type in pokemon.Types)
        {
            SetOneHot(features, offset, orderings.Types, type);
        }

        offset += orderings.Types.Count;

        SetOneHot(features, offset, orderings.Types, pokemon.TeraType);
        offset += orderings.Types.Count;
        features[offset++] = ToFloat(pokemon.Terastallized);

        for (var i = 0; i < orderings.Stats.Count; i++)
        {
            features[offset + i] = (float)(pokemon.BaseStats.GetValueOrDefault(orderings.Stats[i]) / BASE_STAT_SCALE);
        }

        offset += orderings.Stats.Count;
        features[offset++] = (float)(pokemon.Level / LEVEL_SCALE);
        features[offset++] = ToFloat(pokemon.Item != TrackedPokemon.UNKNOWN_ITEM);
        features[offset++] = ToFloat(pokemon.Ability != null);

        for (var i = 0; i < orderings.Effects.Count; i++)
        {
            features[offset + i] = ToFloat(pokemon.Effects.Contains(orderings.Effects[i]));
        }

        offset += orderings.Effects.Count;
        features[offset++] = ToFloat(pokemon.MustRecharge);
        features[offset++] = ToFloat(pokemon.FirstTurn);
        features[offset++] = ToFloat(pokemon.Preparing);
        features[offset++] = (float)(Math.Min(pokemon.Moves.Count, BattleObservation.MoveCount) /
                                     (double)BattleObservation.MoveCount);
        EnsureBlockSize("mon", offset - start, Spec.MonFeatureSize);
    }

    protected override void WriteMoveFeatures(float[] features, int start, TrackedMove move, TrackedPokemon owner,
        TrackedPokemon target)
    {
        var orderings = Spec.Orderings;
        if (!Dex.TryGetMoveEntry(move.Id, out var entry))
        {
            throw new InvalidOperationException($"No data for move {move.Id}");
        }

        features[start] = 1f;
        features[start + 1] = (float)(Math.Min(entry.BasePower, MAX_BASE_POWER) / BASE_POWER_SCALE);
        features[start + 2] = (float)(entry.Accuracy.HasValue ? entry.Accuracy.Value / 100.0 : 1.0);
        features[start + 3] = move.MaxPp != 0 ? (float)((double)move.CurrentPp / move.MaxPp) : 1f;
        features[start + 4] = (float)(Math.Clamp(entry.Priority, MIN_PRIORITY, MAX_PRIORITY) / (double)MAX_PRIORITY);
        var offset = start + 5;

        var category = entry.Category.ToUpperInvariant();
        SetOneHot(features, offset, orderings.Categories, category);
        offset += orderings.Categories.Count;

        var type = PokeEnvNames.TypeFromName(entry.Type);
        SetOneHot(features, offset, orderings.Types, type);
        offset += orderings.Types.Count;

        if (target != null && category != STATUS_CATEGORY)
        {
            var multiplier = DamageMultiplier(type, target);
            features[offset] = multiplier == 0
                ? 0f
                : (float)(Math.Clamp(Math.Log2(multiplier), -MAX_EFFECTIVENESS_LOG, MAX_EFFECTIVENESS_LOG) /
                          MAX_EFFECTIVENESS_LOG);
            features[offset + 1] = ToFloat(multiplier == 0);
        }

        features[offset + 2] = ToFloat(owner.Types.Contains(type));
        offset += 3;

        features[offset] = (float)Fraction(entry.Heal);
        features[offset + 1] = (float)Fraction(entry.Drain);
        features[offset + 2] = (float)Recoil(entry);
        features[offset + 3] = ToFloat(PokeEnvNames.ProtectMoves.Contains(move.Id));
        features[offset + 4] = ToFloat(entry.SelfSwitch);
        features[offset + 5] = ToFloat(!string.IsNullOrEmpty(entry.SideCondition));
        features[offset + 6] = ToFloat(!string.IsNullOrEmpty(entry.Status));
        features[offset + 7] = ToFloat(entry.ForceSwitch);
        offset += 8;
        EnsureBlockSize("move", offset - start, Spec.MoveFeatureSize);
    }

    protected override void WriteField(float[] field, TrackedBattleState state)
    {
        var orderings = Spec.Orderings;
        var offset = 0;
        foreach (var weather in state.Weather.Keys)
        {
            SetOneHot(field, offset, orderings.Weathers, weather);
        }

        offset += orderings.Weathers.Count;
        foreach (var fieldEffect in state.Fields.Keys)
        {
            SetOneHot(field, offset, orderings.Fields, fieldEffect);
        }

        offset += orderings.Fields.Count;
        WriteSideConditions(field, offset, state.SideConditions);
        offset += orderings.SideConditions.Count;
        WriteSideConditions(field, offset, state.OpponentSideConditions);
        offset += orderings.SideConditions.Count;

        var ourAlive = state.Team.Count(pokemon => !pokemon.Fainted);
        var opponentFainted = state.OpponentTeam.Count(pokemon => pokemon.Fainted);
        field[offset] = (float)(Math.Min(state.Turn, MAX_TURN) / (double)MAX_TURN);
        field[offset + 1] = ToFloat(state.CanTera);
        field[offset + 2] = ToFloat(state.UsedTera);
        field[offset + 3] = ToFloat(state.OpponentUsedTera);
        field[offset + 4] = ToFloat(state.ForceSwitch);
        field[offset + 5] = ToFloat(state.Trapped);
        field[offset + 6] = (float)(ourAlive / TEAM_SIZE);
        field[offset + 7] = (float)((TEAM_SIZE - opponentFainted) / TEAM_SIZE);
        field[offset + 8] = (float)(state.OpponentTeam.Count / TEAM_SIZE);
        offset += 9;
        EnsureBlockSize("field", offset, Spec.FieldFeatureSize);
    }

    private void WriteSideConditions(float[] field, int offset, Dictionary<string, int> conditions)
    {
        var sideConditions = Spec.Orderings.SideConditions;
        foreach (var (condition, value) in conditions)
        {
            var index = sideConditions.IndexOf(condition);
            if (index < 0)
            {
                continue;
            }

            field[offset + index] = PokeEnvNames.StackableSideConditions.TryGetValue(condition, out var maxLayers)
                ? (float)(Math.Min(value, maxLayers) / (double)maxLayers)
                : 1f;
        }
    }
}
