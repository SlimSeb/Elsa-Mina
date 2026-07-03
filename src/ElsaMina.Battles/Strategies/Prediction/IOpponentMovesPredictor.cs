namespace ElsaMina.Battles.Strategies.Prediction;

public interface IOpponentMovesPredictor
{
    /// <summary>
    /// Predicts the opponent's active pokemon: the moves it is most likely to carry (combining moves
    /// already revealed in the battle with the most used moves from Smogon usage statistics) and the
    /// nature + EV spread it most commonly runs. Both come from the same cached usage-data fetch.
    /// </summary>
    Task<OpponentPrediction> PredictAsync(string format, string species,
        IReadOnlyCollection<string> revealedMoves, CancellationToken cancellationToken = default);
}
