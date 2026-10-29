namespace ElsaMina.Core.Services.Formats;

public interface IFormatsManager
{
    IEnumerable<string> Formats { get; }
    void ParseFormats(string[] formats);
    string GetCleanFormat(string formatId);

    /// <summary>
    /// Whether the server generates the players' teams in this format (random battles, Battle Factory...). A format
    /// id with custom rules ("gen9ou@@@...") is its base format's.
    /// </summary>
    bool HasGeneratedTeams(string format);
}
