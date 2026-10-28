namespace ElsaMina.Battles;

/// <summary>
/// The battle bot's settings from config.json.
/// </summary>
public interface IBattlesConfiguration
{
    /// <summary>
    /// Where -savereplays writes the bot's battles. Empty: a replays folder next to the bot's binaries.
    /// </summary>
    string ReplaysDirectory { get; }

    /// <summary>
    /// The pokemon-showdown package (its dist/sim) the minipoke strategies search with: one turn ahead on Showdown's
    /// simulator. Empty: they play the model's choice without searching.
    /// </summary>
    string ShowdownDirectory { get; }

    /// <summary>
    /// The Node.js executable that runs the simulator. Empty: "node".
    /// </summary>
    string NodePath { get; }
}
