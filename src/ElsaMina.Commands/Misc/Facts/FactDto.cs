using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Facts;

public class FactDto
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
}