namespace ElsaMina.Commands.Alerts;

public static class AlertArguments
{
    private static readonly char[] SEPARATORS = [',', ' '];

    /// <summary>
    /// Parses "platform channel" or "platform, channel". The channel keeps its case
    /// since some identifiers (like YouTube channel ids) are case-sensitive.
    /// </summary>
    public static bool TryParse(string target, out string platformInput, out string channelInput)
    {
        platformInput = null;
        channelInput = null;
        var parts = target?.Split(SEPARATORS, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts == null || parts.Length != 2)
        {
            return false;
        }

        platformInput = parts[0];
        channelInput = parts[1];
        return true;
    }
}
