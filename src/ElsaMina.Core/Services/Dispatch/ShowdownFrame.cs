namespace ElsaMina.Core.Services.Dispatch;

public static class ShowdownFrame
{
    /// <summary>
    /// Showdown only omits the <c>&gt;ROOMID</c> header for the lobby and for global messages
    /// (pm, queryresponse, challstr, ...), so a frame without a header is attributed to the lobby.
    /// </summary>
    public const string HEADERLESS_ROOM_ID = "lobby";

    public static string GetRoomId(string frame)
    {
        if (string.IsNullOrEmpty(frame) || frame[0] != '>')
        {
            return HEADERLESS_ROOM_ID;
        }

        var lineEnd = frame.IndexOf('\n');
        var roomId = lineEnd < 0 ? frame[1..] : frame[1..lineEnd];
        roomId = roomId.TrimEnd('\r');
        return string.IsNullOrWhiteSpace(roomId) ? HEADERLESS_ROOM_ID : roomId;
    }
}
