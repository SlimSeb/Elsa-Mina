using ElsaMina.Battles.Dtos;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Tracking;

/// <summary>
/// poke-env's AbstractBattle.get_pokemon: finds the Pokémon a protocol identifier ("p2a: Garchomp") refers to,
/// creating it on first sight.
/// </summary>
public class TrackedPokemonResolver
{
    private readonly IBattleDex _dex;

    public TrackedPokemonResolver(IBattleDex dex)
    {
        _dex = dex;
    }

    public TrackedPokemon Get(TrackedBattleState state, string identifier, bool forceSelfTeam = false,
        string details = "", SidePokemon request = null)
    {
        if (identifier.Length < 4)
        {
            throw new BattleTrackingException($"Unexpected identifier '{identifier}'");
        }

        // "p1a: Name" (position letter) becomes "p1: Name"
        if (identifier[3] != ' ')
        {
            identifier = identifier[..2] + identifier[3..];
            var illusionResolved = ResolveOwnIllusion(state, identifier);
            if (illusionResolved != null)
            {
                return illusionResolved;
            }
        }

        var existing = TrackedBattleState.Find(state.Team, identifier) ??
                       TrackedBattleState.Find(state.OpponentTeam, identifier);
        if (existing != null)
        {
            return existing;
        }

        var playerRole = identifier[..2];
        var name = identifier[3..].Trim();
        var team = playerRole == state.PlayerRole || forceSelfTeam ? state.Team : state.OpponentTeam;

        // A known Pokémon seen under another name (nickname): rename it in place
        var nameFromDetails = details.Split(", ")[0];
        var matches = team.Where(pokemon => pokemon.IdentifiesAs(nameFromDetails)).ToList();
        if (matches.Count >= 2)
        {
            throw new BattleTrackingException($"Several Pokémon identify as {nameFromDetails}");
        }

        if (matches.Count == 1)
        {
            matches[0].Identifier = identifier;
            matches[0].Name = identifier[4..];
            return matches[0];
        }

        if (state.TeamSizes.Count > 0)
        {
            if (!state.TeamSizes.TryGetValue(playerRole, out var teamSize))
            {
                throw new BattleTrackingException($"No team size for {playerRole}");
            }

            if (team.Count >= teamSize)
            {
                throw new BattleTrackingException($"{playerRole}'s team already has {teamSize} Pokémon");
            }
        }

        var created = new TrackedPokemon(_dex, identifier);
        if (request != null)
        {
            created.InitializeFromRequest(request, name);
        }
        else if (!string.IsNullOrEmpty(details))
        {
            created.InitializeFromDetails(details, name);
        }
        else
        {
            created.InitializeFromSpecies(identifier[4..], name);
        }

        team.Add(created);
        return created;
    }

    /// <summary>
    /// When a message names one of our Pokémon as active, but another one is active, a Zoroark is involved:
    /// returns whichever of the two has Illusion, or null to fall back to the normal lookup.
    /// </summary>
    private static TrackedPokemon ResolveOwnIllusion(TrackedBattleState state, string identifier)
    {
        if (identifier[..2] != state.PlayerRole)
        {
            return null;
        }

        var named = TrackedBattleState.Find(state.Team, identifier);
        var believedActive = state.ActivePokemon;
        if (named == null || believedActive == null)
        {
            return null;
        }

        if (believedActive == named || named.HasIllusionAbility)
        {
            return named;
        }

        if (believedActive.HasIllusionAbility)
        {
            return believedActive;
        }

        Log.Debug("Message thinks {0} is active, but it's not", identifier);
        return null;
    }
}
