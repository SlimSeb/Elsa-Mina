using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularAnalyzedInstruction
{
    [JsonProperty("steps")]
    public List<SpoonacularStep> Steps { get; set; }
}
