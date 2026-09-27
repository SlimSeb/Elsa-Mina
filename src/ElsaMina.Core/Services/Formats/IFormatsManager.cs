namespace ElsaMina.Core.Services.Formats;

public interface IFormatsManager
{
    IEnumerable<string> Formats { get; }
    void ParseFormats(string[] formats);
    string GetCleanFormat(string formatId);
}