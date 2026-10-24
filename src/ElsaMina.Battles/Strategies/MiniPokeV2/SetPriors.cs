using System.Security.Cryptography;
using System.Text.Json;
using ElsaMina.Battles.Tracking;

namespace ElsaMina.Battles.Strategies.MiniPokeV2;

/// <summary>
/// setpriors.py: what Showdown's gen 9 random battle sets say about an opponent's Pokémon, read from the
/// set_priors.json exported with a minipoke-v2 model (the sets it was trained with, not Showdown's current ones).
/// Once a Pokémon has shown a move, its ability or its tera type, only some roles of its species are still possible;
/// their pools say which moves it probably also has.
///
/// The rules, as encoder_spec.json's "set_priors" states them:
/// <list type="bullet">
/// <item>Species: its id, else its dex battleOnly form(s), then its base species.</item>
/// <item>Evidence: revealed moves, base ability, item, and tera type once terastallized. Evidence outside the
/// species' pools is ignored. A role is consistent when it contains every piece; none → every role (fallback).</item>
/// <item>Candidates: moves of the roles used not revealed yet, by fraction of those roles with them (descending),
/// ties by id (ordinal), the first K kept.</item>
/// <item>Abilities, items, tera types: mean over the roles used of [x in role] / |role's x|, summed in role order;
/// a revealed one has weight 1.</item>
/// <item>Choice item possible: a revealed Choice item; or an unknown item, no revealed status move but Trick,
/// Switcheroo or Healing Wish, and a role used that can be dealt a set without status moves or has one of those
/// three moves (Showdown's teams.ts getItem).</item>
/// </list>
/// </summary>
public sealed class SetPriors
{
    private const string STATUS_CATEGORY = "Status";
    private const int MOVES_PER_SET = 4;
    private static readonly HashSet<string> CHOICE_ITEMS = ["choiceband", "choicescarf", "choicespecs"];
    // A set with one of these gets a Choice item even with status moves
    private static readonly HashSet<string> CHOICE_STATUS_MOVES = ["healingwish", "switcheroo", "trick"];

    private readonly IReadOnlyDictionary<string, SetPriorsSpecies> _species;
    private readonly IBattleDex _dex;

    public SetPriors(IReadOnlyDictionary<string, SetPriorsSpecies> species, string sha256, IBattleDex dex)
    {
        _species = species;
        Sha256 = sha256;
        _dex = dex;
    }

    /// <summary>
    /// SHA-256 of set_priors.json's bytes, which encoder_spec.json records.
    /// </summary>
    public string Sha256 { get; }

    public static SetPriors Load(string path, IBattleDex dex)
    {
        var bytes = File.ReadAllBytes(path);
        var file = JsonSerializer.Deserialize<SetPriorsFile>(bytes)
                   ?? throw new InvalidOperationException($"Empty set priors at {path}");
        var species = file.Species.ToDictionary(pair => pair.Key, pair => new SetPriorsSpecies(pair.Value));
        return new SetPriors(species, Convert.ToHexStringLower(SHA256.HashData(bytes)), dex);
    }

    /// <summary>
    /// infer_mon: the priors of one of the opponent's Pokémon. Its base ability, because an ability gained in
    /// battle (Trace, Skill Swap) is not part of its set.
    /// </summary>
    public MonSetPrior Infer(TrackedPokemon pokemon, int candidateCount)
    {
        // "" once it is known to hold nothing (knocked off, consumed), null while unknown
        var item = pokemon.Item == TrackedPokemon.UNKNOWN_ITEM ? null : pokemon.Item ?? "";
        var teraType = pokemon.Terastallized && pokemon.TeraType != null ? pokemon.TeraType.ToLowerInvariant() : null;
        return Infer(pokemon.Species, pokemon.Moves.Keys, pokemon.BaseAbility, item, teraType, candidateCount);
    }

    /// <param name="species">The Pokémon's species.</param>
    /// <param name="revealedMoves">Its revealed moves.</param>
    /// <param name="ability">Its revealed base ability, or null.</param>
    /// <param name="item">Its revealed item, "" once it is known to hold none, null while unknown.</param>
    /// <param name="teraType">Its tera type once terastallized, or null.</param>
    /// <param name="candidateCount">How many candidate moves to keep (K).</param>
    public MonSetPrior Infer(string species, IEnumerable<string> revealedMoves, string ability, string item,
        string teraType, int candidateCount)
    {
        var key = KeyFor(species);
        if (key == null)
        {
            return MonSetPrior.NotFound;
        }

        var entry = _species[key];
        var revealed = revealedMoves.Select(ShowdownId.ToId).ToList();
        var moveEvidence = revealed.Where(entry.MovePool.Contains).ToList();
        var abilityEvidence = Evidence(ability, entry.AbilityPool);
        var itemId = string.IsNullOrEmpty(item) ? item : ShowdownId.ToId(item);
        var itemEvidence = Evidence(itemId, entry.ItemPool);
        var teraEvidence = Evidence(teraType, entry.TeraTypePool);

        var consistent = entry.Roles
            .Where(role => moveEvidence.TrueForAll(role.Moves.Contains)
                           && (abilityEvidence == null || role.Abilities.Contains(abilityEvidence))
                           && (itemEvidence == null || role.Items.Contains(itemEvidence))
                           && (teraEvidence == null || role.TeraTypes.Contains(teraEvidence)))
            .ToList();
        var fallback = consistent.Count == 0;
        var used = fallback ? entry.Roles : consistent;

        return new MonSetPrior
        {
            Found = true,
            RoleCount = entry.Roles.Count,
            ConsistentRoleCount = consistent.Count,
            Fallback = fallback,
            Candidates = Candidates(used, revealed.ToHashSet(), candidateCount),
            Abilities = Weights(used, role => role.Abilities, abilityEvidence),
            Items = entry.ItemPool.Count > 0 ? Weights(used, role => role.Items, itemEvidence) : [],
            TeraTypes = Weights(used, role => role.TeraTypes, teraEvidence),
            ChoicePossible = IsChoicePossible(itemId, revealed, used)
        };
    }

    /// <summary>
    /// The species the sets list this Pokémon under, or null when they have none.
    /// </summary>
    public string KeyFor(string species)
    {
        var id = ShowdownId.ToId(species);
        if (_species.ContainsKey(id))
        {
            return id;
        }

        return _dex.TryGetSpecies(id, out var entry)
            ? entry.BattleOnly.Append(entry.BaseSpecies).Select(ShowdownId.ToId).FirstOrDefault(_species.ContainsKey)
            : null;
    }

    /// <summary>
    /// A piece of evidence only says something about the roles when the species' pool has it.
    /// </summary>
    private static string Evidence(string name, IReadOnlySet<string> pool)
    {
        var id = string.IsNullOrEmpty(name) ? null : ShowdownId.ToId(name);
        return id != null && pool.Contains(id) ? id : null;
    }

    private static List<SetPriorWeight> Candidates(IReadOnlyList<SetPriorsRole> roles, HashSet<string> revealed,
        int candidateCount)
    {
        var counts = new Dictionary<string, int>();
        foreach (var move in roles.SelectMany(role => role.Moves))
        {
            counts[move] = counts.GetValueOrDefault(move) + 1;
        }

        return counts
            .Where(pair => !revealed.Contains(pair.Key))
            .Select(pair => new SetPriorWeight(pair.Key, (double)pair.Value / roles.Count))
            .OrderByDescending(candidate => candidate.Weight)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .Take(candidateCount)
            .ToList();
    }

    private static List<SetPriorWeight> Weights(IReadOnlyList<SetPriorsRole> roles,
        Func<SetPriorsRole, List<string>> values, string revealed)
    {
        if (revealed != null)
        {
            return [new SetPriorWeight(revealed, 1.0)];
        }

        var weights = new Dictionary<string, double>();
        foreach (var role in roles)
        {
            var roleValues = values(role);
            foreach (var value in roleValues)
            {
                // Summed in this order, like Python, so the float64 rounding is the same
                weights[value] = weights.GetValueOrDefault(value) + 1.0 / roleValues.Count / roles.Count;
            }
        }

        return weights
            .Select(pair => new SetPriorWeight(pair.Key, pair.Value))
            .OrderByDescending(weight => weight.Weight)
            .ThenBy(weight => weight.Name, StringComparer.Ordinal)
            .ToList();
    }

    private bool IsChoicePossible(string item, List<string> revealedMoves, IReadOnlyList<SetPriorsRole> roles)
    {
        if (item != null)
        {
            // A revealed item, or none left
            return CHOICE_ITEMS.Contains(item);
        }

        var hasStatusMove = revealedMoves.Exists(move => !IsDamaging(move) && !CHOICE_STATUS_MOVES.Contains(move));
        return !hasStatusMove && roles.Any(CanBeDealtChoiceItem);
    }

    private bool CanBeDealtChoiceItem(SetPriorsRole role)
    {
        var damagingMoves = role.Moves.Count(IsDamaging);
        return damagingMoves >= Math.Min(MOVES_PER_SET, role.Moves.Count) ||
               role.Moves.Exists(CHOICE_STATUS_MOVES.Contains);
    }

    private bool IsDamaging(string moveId) =>
        _dex.TryGetMove(moveId, out var move) && move.Category != STATUS_CATEGORY;
}
