namespace ElsaMina.Battles.Strategies;

public enum BattleDecisionStrategy
{
    Llm,
    DamageCalc,
    TypeMatchup,
    Random,

    /// <summary>
    /// The first minipoke model, ~1M parameters (Models/MiniPokeV1_1M, strategy "minipokev1-1m"). Each model gets
    /// its own value, named after its model directory as MiniPokeV{version}_{size}, and a line in BattlesModule.
    /// </summary>
    MiniPokeV1_1M,

    /// <summary>
    /// minipoke v1 with ~4M parameters: same encoder as <see cref="MiniPokeV1_1M"/>, larger network.
    /// </summary>
    MiniPokeV1_4M
}
