namespace ElsaMina.Battles.Strategies;

public enum BattleDecisionStrategy
{
    /// <summary>
    /// Searches a damage-calc simulation of the turn. Also what the minipoke strategies fall back on.
    /// </summary>
    DamageCalc,

    /// <summary>
    /// minipoke v3 with ~22M parameters (Models/MiniPokeV3_22M, strategy "minipokev3-22m"): encoder_v2.py's
    /// observation with history and move effect features, trained on human ladder replays (advantage-weighted BC).
    /// Played with the V2 encoder. Each model gets its own value, named after its model directory as
    /// MiniPokeV{version}_{size} (a minor version such as 2.1 written 2_1), and a line in BattlesModule.
    /// </summary>
    MiniPokeV3_22M,

    // Search variants, "<model>+search": the same model, searching one turn ahead on Showdown's simulator
    // (MiniPokeTurnSearch). Named after their model with a _Search suffix: BattlesModule registers each with its model.
    // Listed only when the simulator is configured (ShowdownDirectory).

    /// <summary>
    /// <see cref="MiniPokeV3_22M"/> with search ("minipokev3-22m+search"). The strategy the bot starts with.
    /// </summary>
    MiniPokeV3_22M_Search
}
