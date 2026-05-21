namespace ElsaMina.Commands.ChatLog;

public class DayLineCountRow
{
    public required string UserId { get; init; }
    public required string Color { get; init; }
    public required int Messages { get; init; }
    public required int Words { get; init; }
    public required int Chars { get; init; }
    public double WordsPerMessage => (double)Words / Messages;
    public double CharsPerMessage => (double)Chars / Messages;
}
