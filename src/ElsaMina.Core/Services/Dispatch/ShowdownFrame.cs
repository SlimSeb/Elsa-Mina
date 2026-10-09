namespace ElsaMina.Core.Services.Dispatch;

public static class ShowdownFrame
{
    /// <summary>
    /// Showdown omet le header <c>&gt;ROOMID</c> seulement pour le lobby et les messages globaux
    /// (pm, queryresponse, challstr...), donc frame sans header => lobby
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
