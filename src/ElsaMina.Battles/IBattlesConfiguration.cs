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
}
