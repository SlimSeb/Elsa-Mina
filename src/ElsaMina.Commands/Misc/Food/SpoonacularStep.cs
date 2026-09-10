using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularStep
{
    [JsonPropertyName("step")]
    public string Step { get; set; }
}
