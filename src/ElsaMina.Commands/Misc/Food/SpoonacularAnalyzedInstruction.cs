using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularAnalyzedInstruction
{
    [JsonPropertyName("steps")]
    public List<SpoonacularStep> Steps { get; set; }
}
