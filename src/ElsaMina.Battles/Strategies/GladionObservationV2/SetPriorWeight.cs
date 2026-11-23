namespace ElsaMina.Battles.Strategies.GladionObservationV2;

/// <summary>
/// A move, ability, item or tera type id and how likely the set priors make it.
/// </summary>
public sealed record SetPriorWeight(string Name, double Weight);
