namespace ElsaMina.Commands.ChatLog;

internal static class ChatLogHelpers
{
    internal static string GetS3Key(string roomId, DateOnly date) =>
        $"chatlogs/{roomId}/{date:yyyy-MM-dd}.txt";

    internal static string GetS3MonthPrefix(string roomId, int year, int month) =>
        $"chatlogs/{roomId}/{year:D4}-{month:D2}-";

    // Format: "[HH:mm:ss UTC] username: message"
    internal static bool TryParseLine(string line, out string username, out string message)
    {
        username = null;
        message = null;
        if (line.Length <= 15 || line[0] != '[')
        {
            return false;
        }

        var rest = line[15..];
        var separatorIndex = rest.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return false;
        }

        username = rest[..separatorIndex];
        message = rest[(separatorIndex + 2)..];
        return true;
    }
}
