namespace ElsaMina.Battles.Strategies.Prediction;

public interface IOpponentMovesPredictor
{
    /// <summary>
    /// Predicts the moves the opponent's pokemon is most likely to carry, combining moves already
    /// revealed in the battle with the most used moves from Smogon usage statistics for the format.
    /// </summary>
    Task<IReadOnlyList<PredictedMove>> PredictMovesAsync(string format, string species,
        IReadOnlyCollection<string> revealedMoves, CancellationToken cancellationToken = default);
}
