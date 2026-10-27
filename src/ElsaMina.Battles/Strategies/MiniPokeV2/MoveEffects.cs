using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Tracking;
using ElsaMina.Core.Services.Dex;

namespace ElsaMina.Battles.Strategies.MiniPokeV2;

/// <summary>
/// effects.py's static features: whether a move can work on its target, from the rules the spec lists (abilities,
/// items, the target's types) and the Pokémon's ability weights. Probabilities are computed in double precision,
/// summed in the weights' order, like Python does.
/// </summary>
public sealed class MoveEffects
{
    private const string STATUS_CATEGORY = "status";
    private const double SUPER_EFFECTIVE = 2.0;

    private readonly EncoderSpecMoveEffects _rules;
    private readonly Func<string, TrackedPokemon, double> _damageMultiplier;

    /// <param name="rules">The spec's move effect rules.</param>
    /// <param name="damageMultiplier">encoder.py's type chart multiplier of a move type (poke-env's name) on a
    /// Pokémon.</param>
    public MoveEffects(EncoderSpecMoveEffects rules, Func<string, TrackedPokemon, double> damageMultiplier)
    {
        _rules = rules;
        _damageMultiplier = damageMultiplier;
    }

    /// <summary>
    /// The Pokémon's ability, else its set priors' ability weights, else its possible abilities, uniformly.
    /// </summary>
    public static IReadOnlyList<SetPriorWeight> AbilityWeights(TrackedPokemon pokemon, MonSetPrior prior)
    {
        if (pokemon == null)
        {
            return [];
        }

        if (pokemon.Ability != null)
        {
            return [new SetPriorWeight(ShowdownId.ToId(pokemon.Ability), 1.0)];
        }

        if (prior is { Found: true, Abilities.Count: > 0 })
        {
            return prior.Abilities;
        }

        var possible = pokemon.PossibleAbilities;
        return possible.Select(ability => new SetPriorWeight(ShowdownId.ToId(ability), 1.0 / possible.Count)).ToList();
    }

    /// <summary>
    /// (no effect, ability blocks, status fails) of <paramref name="owner"/>'s move on <paramref name="target"/>.
    /// </summary>
    public (double NoEffect, double AbilityBlocks, double StatusFails) StaticFeatures(string moveId, MoveData entry,
        TrackedPokemon owner, TrackedPokemon target, IReadOnlyList<SetPriorWeight> ownerAbilities,
        IReadOnlyList<SetPriorWeight> targetAbilities, bool neutralizingGas)
    {
        if (target == null)
        {
            return (0, 0, 0);
        }

        var pokeEnvType = PokeEnvNames.TypeFromName(entry.Type);
        var moveType = pokeEnvType.ToLowerInvariant();
        var multiplier = _damageMultiplier(pokeEnvType, target);
        var category = entry.Category.ToLowerInvariant();
        var ownerAbility = owner.Ability == null ? null : ShowdownId.ToId(owner.Ability);
        var sure = multiplier == 0 && (_rules.Damaging.Contains(category) ||
                                       _rules.TypeChartStatusMoves.Contains(moveId));
        sure = sure || (_rules.ItemRules.TryGetValue(target.Item ?? "", out var itemRule) &&
                        Matches(itemRule, moveId, entry, moveType, multiplier, target, ownerAbility));
        sure = sure || _rules.TypeRules.Exists(rule =>
            Matches(rule, moveId, entry, moveType, multiplier, target, ownerAbility));

        var blocks = 0.0;
        if (!neutralizingGas && !_rules.IgnoreAbilityMoves.Contains(moveId))
        {
            var blocking = 0.0;
            foreach (var ability in targetAbilities)
            {
                if (_rules.AbilityRules.TryGetValue(ability.Name, out var rule) &&
                    Matches(rule, moveId, entry, moveType, multiplier, target, ownerAbility))
                {
                    blocking += ability.Weight;
                }
            }

            var breaking = ownerAbilities.Where(ability => _rules.Breakers.Contains(ability.Name))
                .Aggregate(0.0, (total, ability) => total + ability.Weight);
            blocks = blocking * (1.0 - breaking);
        }

        var fails = category == STATUS_CATEGORY && !string.IsNullOrEmpty(entry.Status) &&
                    !_rules.StatusFailExcept.Contains(moveId) && _rules.FoeTargets.Contains(entry.Target) &&
                    target.Status != null;
        return (sure ? 1.0 : blocks, blocks, fails ? 1.0 : 0.0);
    }

    private bool Matches(EncoderSpecMoveEffectRule rule, string moveId, MoveData entry, string moveType,
        double multiplier, TrackedPokemon target, string ownerAbility)
    {
        var category = entry.Category.ToLowerInvariant();
        return (rule.Types == null || rule.Types.Contains(moveType)) &&
               (rule.Categories == null || rule.Categories.Contains(category)) &&
               (rule.Flags == null || rule.Flags.Exists(flag => entry.Flags.ContainsKey(flag))) &&
               (rule.Statuses == null || (!string.IsNullOrEmpty(entry.Status) && rule.Statuses.Contains(entry.Status))) &&
               (rule.Moves == null || rule.Moves.Contains(moveId)) &&
               (rule.Except == null || !rule.Except.Contains(moveId)) &&
               (rule.Targets ?? _rules.FoeTargets).Contains(entry.Target) &&
               (rule.TargetTypes == null ||
                target.Types.Any(type => rule.TargetTypes.Contains(type.ToLowerInvariant()))) &&
               (rule.MinPriority == null || entry.Priority >= rule.MinPriority) &&
               (!rule.NotSuperEffective || (_rules.Damaging.Contains(category) && multiplier < SUPER_EFFECTIVE)) &&
               (ownerAbility == null || rule.UnlessOwner == null || !rule.UnlessOwner.Contains(ownerAbility));
    }
}
