using ElsaMina.Battles.Dtos;
using ElsaMina.Logging;

namespace ElsaMina.Battles.Tracking;

/// <summary>
/// Applies a request to a <see cref="TrackedBattleState"/> exactly like poke-env 0.16.1's Battle.parse_request:
/// updates our Pokémon and works out the available moves and switches.
/// </summary>
public class BattleRequestTracker
{
    private readonly TrackedPokemonResolver _resolver;

    public BattleRequestTracker(TrackedPokemonResolver resolver)
    {
        _resolver = resolver;
    }

    public void Apply(BattleStateDto request, TrackedBattleState state)
    {
        try
        {
            ApplyRequest(request, state);
            state.IsRequestTracked = true;
        }
        catch (Exception exception)
        {
            // poke-env raises and never chooses a move for this request
            state.IsRequestTracked = false;
            Log.Warning("Battle tracking could not apply the request: {0}", exception.Message);
        }
    }

    private void ApplyRequest(BattleStateDto request, TrackedBattleState state)
    {
        state.IsRequestTracked = false;
        state.Wait = request.Wait;
        var side = request.Side ?? new Side();
        state.AvailableMoves = [];
        state.AvailableSwitches = [];
        state.CanTera = false;
        state.Reviving = side.Pokemon.Any(pokemon => pokemon.Reviving);
        state.Trapped = false;
        state.ForceSwitch = request.ForceSwitch.FirstOrDefault();

        if (side.Pokemon.Count > 0)
        {
            state.PlayerRole = side.Pokemon[0].Ident[..2];
        }

        UpdateTeamFromRequest(side, state);

        if (request.Active.Count > 0)
        {
            var activeRequest = request.Active[0];
            if (activeRequest.Trapped)
            {
                state.Trapped = true;
            }

            var activePokemon = state.ActivePokemon;
            if (activePokemon != null)
            {
                state.AvailableMoves.AddRange(activePokemon.AvailableMovesFromRequest(activeRequest));
            }

            if (!string.IsNullOrEmpty(activeRequest.CanTerastallize))
            {
                state.CanTera = true;
            }
        }

        if (state.Trapped)
        {
            return;
        }

        foreach (var ident in side.Pokemon.Select(requestPokemon => requestPokemon.Ident))
        {
            var pokemon = TrackedBattleState.Find(state.Team, ident)
                          ?? throw new BattleTrackingException($"{ident} missing from the team");
            if (state.Reviving)
            {
                if (pokemon.Fainted)
                {
                    state.AvailableSwitches.Add(pokemon);
                }
            }
            else if (!pokemon.Active && !pokemon.Fainted)
            {
                state.AvailableSwitches.Add(pokemon);
            }
        }
    }

    /// <summary>
    /// _update_team_from_request. The request always says which Pokémon is really active: one that a Zoroark's
    /// switch-in wrongly marked active goes back to its own state, and the Zoroark becomes active.
    /// </summary>
    private void UpdateTeamFromRequest(Side side, TrackedBattleState state)
    {
        var falselyActive = new List<TrackedPokemon>();
        var trulyActive = new List<TrackedPokemon>();
        foreach (var requestPokemon in side.Pokemon)
        {
            if (TrackedBattleState.Find(state.Team, requestPokemon.Ident) == null)
            {
                _resolver.Get(state, requestPokemon.Ident, forceSelfTeam: true, details: requestPokemon.Details,
                    request: requestPokemon);
            }

            var pokemon = TrackedBattleState.Find(state.Team, requestPokemon.Ident)
                          ?? throw new BattleTrackingException($"{requestPokemon.Ident} missing from the team");
            if (requestPokemon.Active && !pokemon.Active)
            {
                trulyActive.Add(pokemon);
            }
            else if (!requestPokemon.Active && pokemon.Active)
            {
                falselyActive.Add(pokemon);
            }
        }

        foreach (var illusioned in falselyActive)
        {
            illusioned.WasIllusioned(state.IsNeutralizingGasActive);
        }

        foreach (var illusionist in trulyActive)
        {
            illusionist.SwitchIn();
        }

        foreach (var requestPokemon in side.Pokemon)
        {
            var pokemon = TrackedBattleState.Find(state.Team, requestPokemon.Ident);
            if (pokemon != null)
            {
                pokemon.UpdateFromRequest(requestPokemon);
            }
            else
            {
                _resolver.Get(state, requestPokemon.Ident, forceSelfTeam: true, details: requestPokemon.Details,
                    request: requestPokemon);
            }
        }
    }
}
