using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.UnitTests.Battles.Strategies.MiniPokeV1;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeV2;

/// <summary>
/// Readable names for every position of encoder_v2.py's arrays, so a parity failure says which feature differs:
/// V1's names for the six shared arrays, built from encoder_spec.json for the others.
/// </summary>
public class MiniPokeV2FeatureNames
{
    private static readonly string[] PRIOR_KINDS = ["ability", "item"];
    private static readonly string[] HISTORY_IDS = ["our_last_move", "opponent_last_move"];

    private static readonly string[] HISTORY =
    [
        "opponent_active_turns", "our_active_turns", "opponent_switched_by_choice", "we_switched_by_choice",
        "opponent_protect_counter", "our_protect_counter", "opponent_same_move_twice", "opponent_choice_locked"
    ];

    private static readonly string[] EFFECTS =
        ["no_effect", "ability_blocks", "status_fails", "immune_before", "failed_last_time", "failed_twice"];

    private readonly MiniPokeV1FeatureNames _v1Names;
    private readonly int _candidateMoves;
    private readonly int _abilitiesAndItems;
    private readonly IReadOnlyList<string> _candidate;
    private readonly IReadOnlyList<string> _prior;

    public MiniPokeV2FeatureNames(EncoderSpec spec)
    {
        _v1Names = new MiniPokeV1FeatureNames(spec);
        _candidateMoves = spec.Model.CandidateMoves;
        _abilitiesAndItems = spec.SetPriors.AbilitiesAndItems;
        _candidate = [.._v1Names.Move, "fraction"];
        _prior =
        [
            ..spec.Orderings.Types.Select(type => $"tera_type:{type}"),
            "has_sets", "consistent_roles", "fallback", "choice_possible"
        ];
    }

    public string Describe(string array, int index)
    {
        var candidateFeatures = _candidate.Count;
        var priorSlotSize = PRIOR_KINDS.Length * _abilitiesAndItems;
        return array switch
        {
            "cand_ids" => $"opponent {index / _candidateMoves} candidate {index % _candidateMoves}",
            "cand_feats" => $"opponent {index / (_candidateMoves * candidateFeatures)} candidate " +
                            $"{index / candidateFeatures % _candidateMoves} {_candidate[index % candidateFeatures]}",
            "prior_ids" or "prior_w" => $"opponent {index / priorSlotSize} " +
                                        $"{PRIOR_KINDS[index / _abilitiesAndItems % PRIOR_KINDS.Length]} " +
                                        $"{index % _abilitiesAndItems}",
            "prior_feats" => $"opponent {index / _prior.Count} {_prior[index % _prior.Count]}",
            "hist_ids" => HISTORY_IDS[index],
            "hist_feats" => HISTORY[index],
            "effect_feats" => $"slot {index / (BattleObservation.MoveCount * EFFECTS.Length)} move " +
                              $"{index / EFFECTS.Length % BattleObservation.MoveCount} {EFFECTS[index % EFFECTS.Length]}",
            "cand_effect_feats" => $"opponent {index / (_candidateMoves * EFFECTS.Length)} candidate " +
                                   $"{index / EFFECTS.Length % _candidateMoves} {EFFECTS[index % EFFECTS.Length]}",
            _ => _v1Names.Describe(array, index)
        };
    }
}
