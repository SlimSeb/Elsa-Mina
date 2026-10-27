using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.MiniPokeV1;
using ElsaMina.Battles.Tracking;

namespace ElsaMina.Battles.Strategies.MiniPokeV2;

/// <summary>
/// encoder_v2.py as minipoke-v2 was trained with it: encoder.py's six arrays, untouched
/// (<see cref="MiniPokeV1Encoder"/>), then what the random battle sets say about each revealed opponent Pokémon
/// (<see cref="SetPriors"/>) and, for a model trained with --history, the history features. Must match it value for
/// value: MiniPokeV2ParityTest checks it against battles recorded with encoder_v2.py.
///
/// Opponent rows follow V1's slots 6-11; an unrevealed slot or a species without sets stays all zeros:
/// <list type="bullet">
/// <item>cand_ids [6, K], cand_feats [6, K, F_MOVE + 1]: candidate moves, with V1's move features (owner = that
/// Pokémon, target = our active, full PP), then the fraction of the roles used that have them.</item>
/// <item>prior_ids, prior_w [6, 2, A]: abilities (row 0) and items (row 1) with their weights.</item>
/// <item>prior_feats [6, types + 4]: tera type weights, has sets, consistent roles / roles, fallback, Choice item
/// possible.</item>
/// <item>hist_ids [2]: the last move of our active, of theirs. hist_feats [8]: turns since their / our active came
/// in, they / we switched by choice last turn, their / our protect counter, they chose the same move twice in a row,
/// that and a Choice item is possible.</item>
/// <item>effect_feats [12, 4, 6], cand_effect_feats [6, K, 6] (--move-effects): whether each known and candidate
/// move can work on the opposing active (<see cref="MoveEffects"/>), and whether it had no effect or failed before
/// (<see cref="MoveOutcomes"/>).</item>
/// </list>
/// History tokens and matchup tokens are not ported: a spec using them is refused.
/// </summary>
public class MiniPokeV2Encoder : MiniPokeV1Encoder
{
    private const int ENCODER_VERSION = 2;
    private const string INT64 = "int64";
    private const string FLOAT32 = "float32";
    private const int PRIOR_ROWS = 2;
    private const int ITEM_ROW = 1;
    private const int PRIOR_TRAILING_FEATURES = 4;
    private const int HISTORY_IDS = 2;
    private const int HISTORY_FEATURES = 8;
    private const int MAX_ACTIVE_TURNS = 10;
    private const int MAX_PROTECT_COUNTER = 3;
    private const int EFFECT_FEATURES = 6;

    private readonly SetPriors _priors;
    private readonly int _candidateMoves;
    private readonly int _abilitiesAndItems;
    private readonly bool _history;
    private readonly MoveEffects _moveEffects;

    public MiniPokeV2Encoder(MiniPokeModelOptions options, IBattleDex dex) : base(options, dex)
    {
        if (Spec == null)
        {
            return;
        }

        if (Spec.EncoderVersion != ENCODER_VERSION || Spec.Model == null || Spec.SetPriors == null)
        {
            throw new InvalidOperationException(
                $"{options.EncoderSpecPath} is not a minipoke-v2 spec (encoder_version {Spec.EncoderVersion})");
        }

        if (Spec.Model.HistoryTokens > 0 || Spec.Model.MatchupTokens)
        {
            throw new NotSupportedException(
                $"{options.Strategy} reads history or matchup tokens, which {nameof(MiniPokeV2Encoder)} does not write");
        }

        _candidateMoves = Spec.Model.CandidateMoves;
        _abilitiesAndItems = Spec.SetPriors.AbilitiesAndItems;
        _history = Spec.Model.History;
        if (Spec.Model.MoveEffects)
        {
            if (Spec.MoveEffects?.Features.Count != EFFECT_FEATURES)
            {
                throw new InvalidOperationException(
                    $"{options.EncoderSpecPath} reads move effects without the {EFFECT_FEATURES} features' rules");
            }

            _moveEffects = new MoveEffects(Spec.MoveEffects, DamageMultiplier);
        }

        _priors = SetPriors.Load(Path.Join(options.ModelDirectory, Spec.SetPriors.File), dex);
        if (_priors.Sha256 != Spec.SetPriors.Sha256)
        {
            throw new InvalidOperationException(
                $"{Spec.SetPriors.File} of {options.Strategy} is not the one its spec was exported with");
        }

        RequireExtraInputs();
    }

    private int CandidateFeatureSize => Spec.MoveFeatureSize + 1;
    private int PriorFeatureSize => Spec.Orderings.Types.Count + PRIOR_TRAILING_FEATURES;

    protected override void WriteExtraInputs(BattleObservation observation, TrackedBattleState state)
    {
        var opponents = state.OpponentTeam.Take(BattleObservation.SlotCount).ToList();
        var priors = opponents.Select(pokemon => _priors.Infer(pokemon, _candidateMoves)).ToList();
        WritePriors(observation, opponents, priors, state.ActivePokemon);
        if (_history)
        {
            var opponentActiveIndex = opponents.IndexOf(state.OpponentActivePokemon);
            WriteHistory(observation, state, opponentActiveIndex >= 0 && priors[opponentActiveIndex].ChoicePossible);
        }

        if (_moveEffects != null)
        {
            WriteMoveEffects(observation, state, opponents, priors);
        }
    }

    /// <summary>
    /// encode_effects: owner = the Pokémon with the move, target = the opposing active (V1's move features).
    /// </summary>
    private void WriteMoveEffects(BattleObservation observation, TrackedBattleState state,
        List<TrackedPokemon> opponents, List<MonSetPrior> priors)
    {
        const int slots = BattleObservation.SlotCount;
        const int moveCount = BattleObservation.MoveCount;
        var features = new float[BattleObservation.MonCount * moveCount * EFFECT_FEATURES];
        var candidateFeatures = new float[slots * _candidateMoves * EFFECT_FEATURES];
        var outcomes = MoveOutcomes.Read(state.ProtocolLines);
        var ourRole = state.PlayerRole;
        var opponentRole = ProtocolHistory.OpponentRole(ourRole);
        var opponentPriors = opponents.Zip(priors).ToDictionary(pair => pair.First, pair => pair.Second);
        var ourActive = state.ActivePokemon;
        var opponentActive = state.OpponentActivePokemon;

        void Write(float[] target, int start, string moveId, TrackedPokemon pokemon, bool ours)
        {
            var side = ours ? ourRole : opponentRole;
            var targetSide = ours ? opponentRole : ourRole;
            var opposing = ours ? opponentActive : ourActive;
            if (!Dex.TryGetMoveEntry(moveId, out var entry))
            {
                throw new InvalidOperationException($"No data for move {moveId}");
            }

            var (noEffect, abilityBlocks, statusFails) = _moveEffects.StaticFeatures(moveId, entry, pokemon,
                opposing, Weights(pokemon), Weights(opposing), state.IsNeutralizingGasActive);
            target[start] = (float)noEffect;
            target[start + 1] = (float)abilityBlocks;
            target[start + 2] = (float)statusFails;
            if (opposing != null)
            {
                target[start + 3] = ToFloat(outcomes.ImmuneBefore(side, moveId, targetSide, NameOf(opposing)));
            }

            target[start + 4] = ToFloat(outcomes.FailedLast(side, NameOf(pokemon), moveId, 1));
            target[start + 5] = ToFloat(outcomes.FailedLast(side, NameOf(pokemon), moveId, 2));
        }

        IReadOnlyList<SetPriorWeight> Weights(TrackedPokemon pokemon) =>
            MoveEffects.AbilityWeights(pokemon, pokemon == null ? null : opponentPriors.GetValueOrDefault(pokemon));

        foreach (var (first, team, ours) in new[] { (0, state.Team, true), (slots, opponents, false) })
        {
            var members = team.Take(slots).ToList();
            for (var i = 0; i < members.Count; i++)
            {
                var moves = members[i].Moves.Values.Take(moveCount).ToList();
                for (var j = 0; j < moves.Count; j++)
                {
                    Write(features, ((first + i) * moveCount + j) * EFFECT_FEATURES, moves[j].Id, members[i], ours);
                }
            }
        }

        for (var row = 0; row < opponents.Count; row++)
        {
            if (!priors[row].Found)
            {
                continue;
            }

            for (var j = 0; j < priors[row].Candidates.Count; j++)
            {
                var candidate = priors[row].Candidates[j];
                if (Dex.TryGetMove(candidate.Name, out _))
                {
                    Write(candidateFeatures, (row * _candidateMoves + j) * EFFECT_FEATURES, candidate.Name,
                        opponents[row], false);
                }
            }
        }

        observation.ExtraFeatures["effect_feats"] = features;
        observation.ExtraFeatures["cand_effect_feats"] = candidateFeatures;
    }

    /// <summary>
    /// The name in the Pokémon's protocol ident: "p1: Garchomp" -> "Garchomp".
    /// </summary>
    private static string NameOf(TrackedPokemon pokemon)
    {
        var separator = pokemon.Identifier.IndexOf(": ", StringComparison.Ordinal);
        return separator < 0 ? pokemon.Identifier : pokemon.Identifier[(separator + 2)..];
    }

    private void WritePriors(BattleObservation observation, List<TrackedPokemon> opponents,
        List<MonSetPrior> priors, TrackedPokemon ourActive)
    {
        const int slots = BattleObservation.SlotCount;
        var candidateIds = new long[slots * _candidateMoves];
        var candidateFeatures = new float[slots * _candidateMoves * CandidateFeatureSize];
        var priorIds = new long[slots * PRIOR_ROWS * _abilitiesAndItems];
        var priorWeights = new float[priorIds.Length];
        var priorFeatures = new float[slots * PriorFeatureSize];

        for (var row = 0; row < opponents.Count; row++)
        {
            var prior = priors[row];
            if (!prior.Found)
            {
                continue;
            }

            for (var j = 0; j < prior.Candidates.Count; j++)
            {
                var candidate = prior.Candidates[j];
                var flatIndex = row * _candidateMoves + j;
                var start = flatIndex * CandidateFeatureSize;
                candidateIds[flatIndex] = VocabularyId(Spec.Vocabularies.Move, candidate.Name);
                if (Dex.TryGetMove(candidate.Name, out _))
                {
                    WriteMoveFeatures(candidateFeatures, start, Dex.CreateMove(candidate.Name), opponents[row],
                        ourActive);
                }
                else
                {
                    candidateFeatures[start] = 1f;
                }

                candidateFeatures[start + Spec.MoveFeatureSize] = (float)candidate.Weight;
            }

            var abilitiesStart = row * PRIOR_ROWS * _abilitiesAndItems;
            WriteWeights(priorIds, priorWeights, abilitiesStart, prior.Abilities,
                ability => VocabularyId(Spec.Vocabularies.Ability, ability));
            WriteWeights(priorIds, priorWeights, abilitiesStart + ITEM_ROW * _abilitiesAndItems, prior.Items,
                HashedItemId);
            WritePriorFeatures(priorFeatures, row * PriorFeatureSize, prior);
        }

        observation.ExtraIds["cand_ids"] = candidateIds;
        observation.ExtraFeatures["cand_feats"] = candidateFeatures;
        observation.ExtraIds["prior_ids"] = priorIds;
        observation.ExtraFeatures["prior_w"] = priorWeights;
        observation.ExtraFeatures["prior_feats"] = priorFeatures;
    }

    /// <summary>
    /// The first A abilities or items, highest weight first.
    /// </summary>
    private void WriteWeights(long[] ids, float[] weights, int start, IReadOnlyList<SetPriorWeight> values,
        Func<string, long> toId)
    {
        for (var j = 0; j < Math.Min(values.Count, _abilitiesAndItems); j++)
        {
            ids[start + j] = toId(values[j].Name);
            weights[start + j] = (float)values[j].Weight;
        }
    }

    private void WritePriorFeatures(float[] features, int start, MonSetPrior prior)
    {
        var types = Spec.Orderings.Types;
        foreach (var teraType in prior.TeraTypes)
        {
            // Tera type ids ("fire") are poke-env type names in lower case
            var index = types.IndexOf(teraType.Name.ToUpperInvariant());
            if (index >= 0)
            {
                features[start + index] = (float)teraType.Weight;
            }
        }

        var offset = start + types.Count;
        features[offset] = 1f;
        features[offset + 1] = (float)((double)prior.ConsistentRoleCount / prior.RoleCount);
        features[offset + 2] = ToFloat(prior.Fallback);
        features[offset + 3] = ToFloat(prior.ChoicePossible);
    }

    private void WriteHistory(BattleObservation observation, TrackedBattleState state, bool opponentChoicePossible)
    {
        var ourActive = state.ActivePokemon;
        var opponentActive = state.OpponentActivePokemon;
        var lines = state.ProtocolLines;
        var ourRole = state.PlayerRole;
        var opponentRole = ProtocolHistory.OpponentRole(ourRole);
        var previousTurn = ProtocolHistory.PreviousTurn(lines);
        var sameMoveTwice = ProtocolHistory.SameMoveTwice(lines, opponentRole);

        observation.ExtraIds["hist_ids"] = [LastMoveId(ourActive), LastMoveId(opponentActive)];
        observation.ExtraFeatures["hist_feats"] =
        [
            ActiveTurnsFeature(opponentActive),
            ActiveTurnsFeature(ourActive),
            ToFloat(ProtocolHistory.SwitchedByChoice(previousTurn, opponentRole)),
            ToFloat(ProtocolHistory.SwitchedByChoice(previousTurn, ourRole)),
            ProtectCounterFeature(opponentActive),
            ProtectCounterFeature(ourActive),
            ToFloat(sameMoveTwice),
            ToFloat(sameMoveTwice && opponentChoicePossible)
        ];
    }

    private long LastMoveId(TrackedPokemon pokemon)
    {
        var lastMove = pokemon?.LastMove;
        return lastMove == null ? Spec.PadId : VocabularyId(Spec.Vocabularies.Move, lastMove.Id);
    }

    private static float ActiveTurnsFeature(TrackedPokemon pokemon) =>
        (float)(Math.Min(pokemon?.ActiveTurns ?? 0, MAX_ACTIVE_TURNS) / (double)MAX_ACTIVE_TURNS);

    private static float ProtectCounterFeature(TrackedPokemon pokemon) => pokemon == null
        ? 0f
        : (float)(Math.Min(pokemon.ProtectCounter, MAX_PROTECT_COUNTER) / (double)MAX_PROTECT_COUNTER);

    /// <summary>
    /// Fails at startup when the spec's inputs after the shared six aren't exactly the ones this encoder writes.
    /// </summary>
    private void RequireExtraInputs()
    {
        const int slots = BattleObservation.SlotCount;
        var written = new List<(string Name, string DataType, int[] Dimensions)>
        {
            ("cand_ids", INT64, [slots, _candidateMoves]),
            ("cand_feats", FLOAT32, [slots, _candidateMoves, CandidateFeatureSize]),
            ("prior_ids", INT64, [slots, PRIOR_ROWS, _abilitiesAndItems]),
            ("prior_w", FLOAT32, [slots, PRIOR_ROWS, _abilitiesAndItems]),
            ("prior_feats", FLOAT32, [slots, PriorFeatureSize])
        };
        if (_history)
        {
            written.Add(("hist_ids", INT64, [HISTORY_IDS]));
            written.Add(("hist_feats", FLOAT32, [HISTORY_FEATURES]));
        }

        if (_moveEffects != null)
        {
            written.Add(("effect_feats", FLOAT32, [BattleObservation.MonCount, BattleObservation.MoveCount,
                EFFECT_FEATURES]));
            written.Add(("cand_effect_feats", FLOAT32, [slots, _candidateMoves, EFFECT_FEATURES]));
        }

        var expected = Spec.ExtraInputs
            .Select(input => Describe(input.Name, input.DataType, input.GetDimensions()))
            .ToList();
        var actual = written.Select(input => Describe(input.Name, input.DataType, input.Dimensions)).ToList();
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"Encoder spec expects inputs [{string.Join(", ", expected)}] after the shared six; " +
                $"{nameof(MiniPokeV2Encoder)} writes [{string.Join(", ", actual)}]");
        }
    }

    private static string Describe(string name, string dataType, int[] dimensions) =>
        $"{name} {dataType}[{string.Join(", ", dimensions)}]";
}
