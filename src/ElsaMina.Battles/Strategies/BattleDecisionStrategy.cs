namespace ElsaMina.Battles.Strategies;

public enum BattleDecisionStrategy
{
    /// <summary>
    /// Searches a damage-calc simulation of the turn. Also what the Gladion strategies fall back on.
    /// </summary>
    DamageCalc,

    /// <summary>
    /// Gladion v1 with ~22M parameters (Models/GladionV1_22M, strategy "gladionv1-22m"): encoder_v2.py's
    /// observation with history and move effect features, trained on human ladder replays (advantage-weighted BC).
    /// Played with the V2 encoder. Each model gets its own value, named after its model directory as
    /// GladionV{version}_{size} (a minor version such as 1.1 written 1_1), and a line in BattlesModule.
    /// </summary>
    GladionV1_22M,

    // Search variants, "<model>+search": the same model, searching one turn ahead on Showdown's simulator
    // (GladionTurnSearch). Named after their model with a _Search suffix: BattlesModule registers each with its model.
    // Listed only when the simulator is configured (ShowdownDirectory).

    /// <summary>
    /// <see cref="GladionV1_22M"/> with search ("gladionv1-22m+search"). The strategy the bot starts with.
    /// </summary>
    GladionV1_22M_Search
}
