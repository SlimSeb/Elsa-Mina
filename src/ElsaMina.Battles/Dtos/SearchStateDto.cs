using System.Text.Json.Serialization;

namespace ElsaMina.Battles.Dtos;

public sealed class SearchStateDto
{
    [JsonPropertyName("searching")]
    public List<string> Searching { get; set; } = [];

    /// <summary>
    /// The games the user plays, by room id. Null when there are none.
    /// </summary>
    [JsonPropertyName("games")]
    public Dictionary<string, string> Games { get; set; }
}
