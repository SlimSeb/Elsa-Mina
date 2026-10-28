using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Strategies.MiniPokeSearch;

/// <summary>
/// One simulated turn as our side sees it, and the winner when it ended the battle ("p1", "p2", "tie").
/// </summary>
public sealed record SimulationLeaf(
    [property: JsonPropertyName("lines")] List<string> Lines,
    [property: JsonPropertyName("winner")] string Winner);
