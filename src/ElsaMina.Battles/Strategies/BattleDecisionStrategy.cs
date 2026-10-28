namespace ElsaMina.Battles.Strategies;

public enum BattleDecisionStrategy
{
    /// <summary>
    /// Searches a damage-calc simulation of the turn. Also what the minipoke strategies fall back on.
    /// </summary>
    DamageCalc,

    /// <summary>
    /// The first minipoke model, ~1M parameters (Models/MiniPokeV1_1M, strategy "minipokev1-1m"). Each model gets
    /// its own value, named after its model directory as MiniPokeV{version}_{size} (version 2.1 as 2_1), and a line
    /// in BattlesModule; its search variant (below) gets a value too.
    /// </summary>
    MiniPokeV1_1M,

    /// <summary>
    /// minipoke v1 with ~4M parameters: same encoder as <see cref="MiniPokeV1_1M"/>, larger network.
    /// </summary>
    MiniPokeV1_4M,

    /// <summary>
    /// minipoke-v2 with ~1M parameters: V1's inputs plus random battle set priors and history features
    /// (encoder_v2.py), trained with the medium recipe.
    /// </summary>
    MiniPokeV2_1M,

    /// <summary>
    /// minipoke-v2 with ~4M parameters: same encoder as <see cref="MiniPokeV2_1M"/>, wide-medium network.
    /// The strategy the bot starts with.
    /// </summary>
    MiniPokeV2_4M,

    /// <summary>
    /// minipoke v2.1 with ~1M parameters (strategy "minipokev2.1-1m"): <see cref="MiniPokeV2_1M"/> trained further
    /// with the move effect features (encoder_v2.py --move-effects: abilities, items and types a move can't work
    /// through, and the moves that had no effect or failed before). Played with the V2 encoder, which reads them from
    /// the spec.
    /// </summary>
    MiniPokeV2_1_1M,

    // Search variants, "<model>+search": the same model, searching one turn ahead on Showdown's simulator
    // (MiniPokeTurnSearch). Named after their model with a _Search suffix: BattlesModule registers each with its model.
    // Listed only when the simulator is configured (ShowdownDirectory).

    /// <summary>
    /// <see cref="MiniPokeV1_1M"/> with search ("minipokev1-1m+search").
    /// </summary>
    MiniPokeV1_1M_Search,

    /// <summary>
    /// <see cref="MiniPokeV1_4M"/> with search ("minipokev1-4m+search").
    /// </summary>
    MiniPokeV1_4M_Search,

    /// <summary>
    /// <see cref="MiniPokeV2_1M"/> with search ("minipokev2-1m+search"), the model search was measured with.
    /// </summary>
    MiniPokeV2_1M_Search,

    /// <summary>
    /// <see cref="MiniPokeV2_4M"/> with search ("minipokev2-4m+search").
    /// </summary>
    MiniPokeV2_4M_Search,

    /// <summary>
    /// <see cref="MiniPokeV2_1_1M"/> with search ("minipokev2.1-1m+search").
    /// </summary>
    MiniPokeV2_1_1M_Search
}
