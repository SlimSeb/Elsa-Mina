using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularSearchResponse
{
    [JsonPropertyName("results")]
    public List<SpoonacularRecipe> Results { get; set; }
}
