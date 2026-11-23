namespace ElsaMina.Battles.Strategies.Gladion;

public interface IGladionModel
{
    bool IsLoaded { get; }

    /// <summary>Returns masked logits [14] and the value estimate for one observation.</summary>
    (float[] Logits, float Value) Evaluate(BattleObservation observation);
}
