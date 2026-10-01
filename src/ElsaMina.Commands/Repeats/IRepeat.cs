namespace ElsaMina.Commands.Repeats;

public interface IRepeat
{
    string RoomId { get; }
    Guid RepeatId { get; }
    string Message { get; }
    TimeSpan Interval { get; }
}