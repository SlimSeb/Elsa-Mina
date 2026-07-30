namespace ElsaMina.Commands.Dolls;

/// <summary>
/// A doll from the Google Drive catalogue. The size comes from the folder the PNG lives in,
/// and is kept as is when the doll is displayed on a profile.
/// </summary>
public class Doll
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Size { get; init; }
    public required string Image { get; init; }
}
