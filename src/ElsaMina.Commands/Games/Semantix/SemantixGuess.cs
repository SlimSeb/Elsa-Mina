namespace ElsaMina.Commands.Games.Semantix;

public class SemantixGuess
{
    public required string Word { get; init; }
    public required double Similarity { get; init; }
    public required int Temperature { get; init; }
    public required int Order { get; init; }
}
