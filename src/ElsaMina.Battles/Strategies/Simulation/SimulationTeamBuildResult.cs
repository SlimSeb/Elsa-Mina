using Lusamine.DamageCalc;

namespace ElsaMina.Battles.Strategies.Simulation;

/// <summary>
/// Intermediate result of building our team for the simulation: the simulation members themselves,
/// the calc pokemon they were built from (needed for the opponent's damage calculations), which member
/// is the active one, and the terastallized variant of that active member when Terastallization is available.
/// </summary>
internal sealed record SimulationTeamBuildResult(
    List<SimulationTeamMember> Members,
    List<Pokemon> MemberPokemons,
    int ActiveMemberIndex,
    Pokemon TerastallizedActivePokemon);
