using System.IO.Hashing;
using System.Text;
using ElsaMina.Battles.Data;
using ElsaMina.Battles.Tracking;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Strategies.MiniPoke;

/// <summary>
/// What every minipoke encoder.py shares, ported over the poke-env-like state in <see cref="BattleContext.Tracked"/>:
/// the 12 Pokémon slots (ours in first-seen order, then the opponent's revealed ones), their species, item, ability
/// and move ids, the 14-action mask, and how actions map to the current request. A model version only adds how
/// it writes its feature vectors (_mon_features, _move_features and the field features of its encoder.py).
///
/// Values are computed in double precision, then stored as float like numpy does when it writes a Python float
/// into a float32 array.
/// </summary>
public abstract class MiniPokeEncoder : IMiniPokeEncoder
{
    private const int SWITCH_ACTIONS = 6;
    private const int FIRST_MOVE_ACTION = 6;
    private const int FIRST_TERA_ENV_ACTION = 22;
    private const int ENV_ACTION_COUNT = 26;
    private const int ENV_TERA_OFFSET = 16;

    private readonly long _firstHashedItemId;

    protected MiniPokeEncoder(MiniPokeModelOptions options, IBattleDex dex)
    {
        Dex = dex;
        if (!File.Exists(options.EncoderSpecPath))
        {
            Log.Warning("Encoder spec not found at {Path}; {Strategy} disabled", options.EncoderSpecPath,
                options.Strategy);
            return;
        }

        Spec = EncoderSpec.Load(options.EncoderSpecPath);
        _firstHashedItemId = Math.Max(Spec.PadId, Spec.UnknownId) + 1;
    }

    /// <summary>
    /// The spec exported with the model, or null when it is missing (nothing gets encoded).
    /// </summary>
    protected EncoderSpec Spec { get; }

    protected IBattleDex Dex { get; }

    public BattleObservation Encode(BattleContext context)
    {
        var state = context.Tracked;
        if (Spec == null || !state.IsRequestTracked || context.ActiveSlots.Count > 1)
        {
            return null;
        }

        var observation = new BattleObservation(Spec.MonFeatureSize, Spec.MoveFeatureSize, Spec.FieldFeatureSize);
        var ourActive = state.ActivePokemon;
        var opponentActive = state.OpponentActivePokemon;

        for (var slot = 0; slot < BattleObservation.MonCount; slot++)
        {
            var isMine = slot < BattleObservation.SlotCount;
            var team = isMine ? state.Team : state.OpponentTeam;
            var teamIndex = isMine ? slot : slot - BattleObservation.SlotCount;
            var pokemon = teamIndex < team.Count ? team[teamIndex] : null;

            observation.MonIds[slot * 3] = SpeciesId(pokemon);
            observation.MonIds[slot * 3 + 1] = ItemId(pokemon);
            observation.MonIds[slot * 3 + 2] = AbilityId(pokemon);
            WriteMonFeatures(observation.MonFeatures, slot * Spec.MonFeatureSize, pokemon, isMine);
            if (pokemon == null)
            {
                continue;
            }

            var target = isMine ? opponentActive : ourActive;
            var moves = pokemon.Moves.Values.Take(BattleObservation.MoveCount).ToList();
            for (var moveIndex = 0; moveIndex < moves.Count; moveIndex++)
            {
                var flatIndex = slot * BattleObservation.MoveCount + moveIndex;
                observation.MoveIds[flatIndex] = VocabularyId(Spec.Vocabularies.Move, moves[moveIndex].Id);
                WriteMoveFeatures(observation.MoveFeatures, flatIndex * Spec.MoveFeatureSize, moves[moveIndex],
                    pokemon, target);
            }
        }

        WriteField(observation.Field, state);
        WriteMask(observation.Mask, state);
        WriteExtraInputs(observation, state);
        MapTeamSlots(observation, context);
        MapMoveSlots(observation, context);
        return observation;
    }

    /// <summary>
    /// _mon_features. <paramref name="pokemon"/> is null for an unrevealed opponent slot (or a missing one of ours).
    /// </summary>
    protected abstract void WriteMonFeatures(float[] features, int start, TrackedPokemon pokemon, bool isMine);

    /// <summary>
    /// _move_features. <paramref name="target"/> is the opposing active Pokémon, if any.
    /// </summary>
    protected abstract void WriteMoveFeatures(float[] features, int start, TrackedMove move, TrackedPokemon owner,
        TrackedPokemon target);

    /// <summary>
    /// The field vector: weather, terrains, side conditions, counters.
    /// </summary>
    protected abstract void WriteField(float[] field, TrackedBattleState state);

    /// <summary>
    /// The inputs a version adds after the six every version shares (<see cref="BattleObservation.ExtraIds"/>,
    /// <see cref="BattleObservation.ExtraFeatures"/>): minipoke-v2's set priors and history.
    /// </summary>
    protected virtual void WriteExtraInputs(BattleObservation observation, TrackedBattleState state)
    {
        // V1 models read the six shared inputs only
    }

    /// <summary>
    /// Fails loudly at startup when the spec's feature sizes aren't the ones this encoder writes: a mismatch would
    /// not crash anything later, the model would just play badly.
    /// </summary>
    protected void RequireFeatureSizes(int monFeatures, int moveFeatures, int fieldFeatures)
    {
        if (Spec != null && (Spec.MonFeatureSize, Spec.MoveFeatureSize, Spec.FieldFeatureSize) !=
            (monFeatures, moveFeatures, fieldFeatures))
        {
            throw new InvalidOperationException(
                $"Encoder spec has f_mon {Spec.MonFeatureSize}, f_move {Spec.MoveFeatureSize} and f_field " +
                $"{Spec.FieldFeatureSize}; {GetType().Name} writes {monFeatures}, {moveFeatures} and {fieldFeatures}");
        }
    }

    private long SpeciesId(TrackedPokemon pokemon)
    {
        return pokemon == null ? Spec.UnknownId : VocabularyId(Spec.Vocabularies.Species, pokemon.Species);
    }

    private long ItemId(TrackedPokemon pokemon)
    {
        if (pokemon == null || pokemon.Item == TrackedPokemon.UNKNOWN_ITEM)
        {
            return Spec.UnknownId;
        }

        return string.IsNullOrEmpty(pokemon.Item) ? Spec.PadId : HashedItemId(pokemon.Item);
    }

    /// <summary>
    /// The id of a known item: items have no vocabulary, only hash buckets.
    /// </summary>
    protected long HashedItemId(string item)
    {
        var hash = Crc32.HashToUInt32(Encoding.UTF8.GetBytes(item));
        return _firstHashedItemId + hash % (uint)Spec.ItemHash.Buckets;
    }

    private long AbilityId(TrackedPokemon pokemon)
    {
        if (pokemon == null)
        {
            return Spec.UnknownId;
        }

        var ability = pokemon.Ability;
        if (ability == null && pokemon.PossibleAbilities.Count == 1)
        {
            ability = pokemon.PossibleAbilities[0];
        }

        return ability == null ? Spec.UnknownId : VocabularyId(Spec.Vocabularies.Ability, ability);
    }

    protected long VocabularyId(Dictionary<string, long> vocabulary, string name)
    {
        return vocabulary.TryGetValue(ShowdownId.ToId(name), out var id) ? id : Spec.UnknownId;
    }

    /// <summary>
    /// Pokemon.damage_multiplier through PokemonType.damage_multiplier: typeless (???) and Stellar attacks or
    /// first types count as neutral. A typeless or Stellar second type makes poke-env raise, and encoder.py then
    /// uses 1.0.
    /// </summary>
    protected static double DamageMultiplier(string moveType, TrackedPokemon target)
    {
        var firstType = target.Type1;
        var secondType = target.Type2;
        if (IsUncharted(moveType) || IsUncharted(firstType) || (secondType != null && IsUncharted(secondType)))
        {
            return 1.0;
        }

        var multiplier = TypeMatchupTable.GetMultiplier(moveType, [firstType]);
        return secondType == null ? multiplier : multiplier * TypeMatchupTable.GetMultiplier(moveType, [secondType]);
    }

    private static bool IsUncharted(string type) =>
        type is PokeEnvNames.THREE_QUESTION_MARKS or PokeEnvNames.STELLAR;

    /// <summary>
    /// A move data fraction ([numerator, denominator], e.g. heal or drain) as poke-env reads it, 0 when absent.
    /// </summary>
    protected static double Fraction(int[] fraction) =>
        fraction is { Length: 2 } ? fraction[0] / (double)fraction[1] : 0.0;

    /// <summary>
    /// poke-env's Move.recoil: the recoil fraction, or 0.25 for Struggle.
    /// </summary>
    protected static double Recoil(MoveData entry)
    {
        if (entry.Recoil is { Length: 2 })
        {
            return Fraction(entry.Recoil);
        }

        return entry.StruggleRecoil ? 0.25 : 0.0;
    }

    protected static void SetOneHot(float[] features, int offset, List<string> ordering, string name)
    {
        var index = name == null ? -1 : ordering.IndexOf(name);
        if (index >= 0)
        {
            features[offset + index] = 1f;
        }
    }

    protected static float ToFloat(bool value) => value ? 1f : 0f;

    protected static void EnsureBlockSize(string block, int written, int expected)
    {
        if (written != expected)
        {
            throw new InvalidOperationException($"Encoded {written} {block} features, the spec expects {expected}");
        }
    }

    /// <summary>
    /// SinglesEnv.get_action_mask (26 gen 9 actions), reduced to encoder.py's 14: switches 0-5, moves 6-9 and
    /// terastallize + move 22-25.
    /// </summary>
    private static void WriteMask(bool[] mask, TrackedBattleState state)
    {
        var actions = new List<int>();
        var active = state.ActivePokemon;
        if (state.Wait)
        {
            actions.Add(0);
        }
        else
        {
            if (!state.Trapped)
            {
                var switchSpecies = state.AvailableSwitches.Select(pokemon => pokemon.BaseSpecies).ToList();
                actions.AddRange(state.Team
                    .Select((pokemon, index) => (pokemon, index))
                    .Where(pair => switchSpecies.Contains(pair.pokemon.BaseSpecies))
                    .Select(pair => pair.index));
            }

            if (active != null)
            {
                actions.AddRange(MoveActions(active, state));
            }
        }

        var envMask = new bool[ENV_ACTION_COUNT];
        foreach (var action in actions.Where(action => action is >= 0 and < ENV_ACTION_COUNT))
        {
            envMask[action] = true;
        }

        Array.Copy(envMask, 0, mask, 0, SWITCH_ACTIONS + BattleObservation.MoveCount);
        Array.Copy(envMask, FIRST_TERA_ENV_ACTION, mask, SWITCH_ACTIONS + BattleObservation.MoveCount,
            BattleObservation.MoveCount);
    }

    private static List<int> MoveActions(TrackedPokemon active, TrackedBattleState state)
    {
        var knownMoves = active.Moves.Values.Take(BattleObservation.MoveCount).ToList();
        var availableIds = state.AvailableMoves.Select(move => move.Id).ToList();
        var moveSpace = knownMoves
            .Select((move, index) => (move, index))
            .Where(pair => availableIds.Contains(pair.move.Id))
            .Select(pair => FIRST_MOVE_ACTION + pair.index)
            .ToList();

        var singleAvailableMove = state.AvailableMoves.Count == 1 ? state.AvailableMoves[0] : null;
        if (moveSpace.Count == 0 && singleAvailableMove != null &&
            !PokeEnvNames.SpecialMoves.Contains(singleAvailableMove.Id))
        {
            moveSpace = [FIRST_MOVE_ACTION];
        }

        var teraSpace = state.CanTera ? moveSpace.Select(action => action + ENV_TERA_OFFSET).ToList() : [];

        // Struggle and recharge come last, so they can't be terastallized
        if (moveSpace.Count == 0 && singleAvailableMove != null &&
            PokeEnvNames.SpecialMoves.Contains(singleAvailableMove.Id))
        {
            moveSpace = [FIRST_MOVE_ACTION];
        }

        return [..moveSpace, ..teraSpace];
    }

    /// <summary>
    /// Switch action i means the i-th Pokémon of our stable team order; /choose switch needs its position in the
    /// current request.
    /// </summary>
    private static void MapTeamSlots(BattleObservation observation, BattleContext context)
    {
        var team = context.Tracked.Team;
        for (var slot = 0; slot < Math.Min(team.Count, BattleObservation.SlotCount); slot++)
        {
            var identifier = team[slot].Identifier;
            observation.TeamSlotToSideIndex[slot] =
                context.SidePokemon.FindIndex(pokemon => pokemon.Ident == identifier);
        }
    }

    /// <summary>
    /// Move action j means what SinglesEnv.action_to_order picks: the j-th known move, or the only available
    /// move when it isn't a known one (Struggle, recharge). /choose move needs its position in the request.
    /// </summary>
    private static void MapMoveSlots(BattleObservation observation, BattleContext context)
    {
        var state = context.Tracked;
        var active = state.ActivePokemon;
        if (active == null || context.ActiveSlots.Count == 0)
        {
            return;
        }

        var knownMoves = active.Moves.Values.Take(BattleObservation.MoveCount).ToList();
        var moves = state.AvailableMoves.Count == 1 && knownMoves.TrueForAll(move => move.Id != state.AvailableMoves[0].Id)
            ? state.AvailableMoves
            : knownMoves;
        var requestMoves = context.ActiveSlots[0].Moves;
        for (var moveIndex = 0; moveIndex < Math.Min(moves.Count, BattleObservation.MoveCount); moveIndex++)
        {
            var id = moves[moveIndex].Id;
            observation.MoveSlotToRequestIndex[moveIndex] = requestMoves.FindIndex(move => move.Id == id);
        }
    }
}
