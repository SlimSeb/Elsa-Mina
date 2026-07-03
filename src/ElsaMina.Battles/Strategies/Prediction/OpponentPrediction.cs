namespace ElsaMina.Battles.Strategies.Prediction;

/// <summary>
/// What we expect of the opponent's active pokemon: the moves it is likely to carry and,
/// when available, the nature + EV spread it most commonly runs. Spread is null when no usage
/// data is available (random battle formats, unknown species, Smogon outage).
/// </summary>
public record OpponentPrediction(IReadOnlyList<PredictedMove> Moves, PredictedSpread Spread)
{
    public static readonly OpponentPrediction Empty = new([], null);
}
