using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;

namespace ElsaMina.BattleAssistant;

/// <summary>
/// The server's settings: appsettings.json, overridden on the command line (--Model MiniPokeV2_1_22M,
/// --ShowdownDirectory ~/showdown/node_modules/pokemon-showdown, --urls http://127.0.0.1:7700).
/// </summary>
public sealed class AssistantOptions : IBattlesConfiguration
{
    /// <summary>
    /// The minipoke model giving the advice, one of the models played with the V2 encoder.
    /// </summary>
    public BattleDecisionStrategy Model { get; set; } = BattleDecisionStrategy.MiniPokeV3_22M;

    /// <summary>
    /// The pokemon-showdown package the one-turn search simulates on. Empty: advice from the model alone.
    /// </summary>
    public string ShowdownDirectory { get; set; } = "";

    /// <summary>
    /// The Node.js executable running the search's simulations. Empty: "node".
    /// </summary>
    public string NodePath { get; set; } = "";

    /// <summary>
    /// Unused: the server saves no replays.
    /// </summary>
    public string ReplaysDirectory => "";
}
