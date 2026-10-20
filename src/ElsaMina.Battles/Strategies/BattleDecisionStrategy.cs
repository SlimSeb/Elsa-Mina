namespace ElsaMina.Battles.Strategies;

public enum BattleDecisionStrategy
{
    Llm,
    DamageCalc,
    TypeMatchup,
    Random,

    /// <summary>
    /// The first minipoke model (Models/MiniPokeV1). Each model version gets its own value, named after its
    /// model directory, and a line in BattlesModule.
    /// </summary>
    MiniPokeV1
}
