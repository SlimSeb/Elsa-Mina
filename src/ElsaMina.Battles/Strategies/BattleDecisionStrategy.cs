namespace ElsaMina.Battles.Strategies;

public enum BattleDecisionStrategy
{
    /// <summary>
    /// Searches a damage-calc simulation of the turn. Also what the minipoke strategies fall back on.
    /// </summary>
    DamageCalc,

    /// <summary>
    /// minipoke v2.1 with ~22M parameters (Models/MiniPokeV2_1_22M): V2's inputs (V1's plus random battle set priors
    /// and history features, encoder_v2.py) and the move effect features (--move-effects), large network, distilled
    /// from a ~4M v2.1 model and trained with the medium recipe. Each model gets its own value, named after its model
    /// directory as MiniPokeV{version}_{size} (version 2.1 as 2_1), and a line in BattlesModule. Only its search
    /// variant, <see cref="MiniPokeV2_1_22M_Search"/>, is offered.
    /// </summary>
    MiniPokeV2_1_22M,

    /// <summary>
    /// minipoke v3 with ~22M parameters (strategy "minipokev3-22m"): V2.1's observation and network, trained on human
    /// ladder replays (advantage-weighted BC) then fine-tuned by PPO anchored to that model. Played with the V2
    /// encoder, like <see cref="MiniPokeV2_1_22M"/>.
    /// </summary>
    MiniPokeV3_22M,

    // Search variants, "<model>+search": the same model, searching one turn ahead on Showdown's simulator
    // (MiniPokeTurnSearch). Named after their model with a _Search suffix: BattlesModule registers each with its model.
    // Listed only when the simulator is configured (ShowdownDirectory).

    /// <summary>
    /// <see cref="MiniPokeV2_1_22M"/> with search ("minipokev2.1-22m+search").
    /// </summary>
    MiniPokeV2_1_22M_Search,

    /// <summary>
    /// <see cref="MiniPokeV3_22M"/> with search ("minipokev3-22m+search"). The strategy the bot starts with.
    /// </summary>
    MiniPokeV3_22M_Search
}
