namespace ElsaMina.Commands.ChatLog;

public interface IChatLogService : IDisposable
{
    void Append(string roomId, long unixTimestamp, string username, string message);
}
