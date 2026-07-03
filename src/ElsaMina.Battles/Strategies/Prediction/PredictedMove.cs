namespace ElsaMina.Battles.Strategies.Prediction;

/// <summary>
/// A move the opponent's active pokemon is expected to carry.
/// Probability is 1.0 for moves already revealed in battle, and the Smogon usage carry rate otherwise.
/// </summary>
public record PredictedMove(string Name, double Probability);
