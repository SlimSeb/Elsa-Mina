using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularRandomResponse
{
    [JsonPropertyName("recipes")]
    public List<SpoonacularRecipe> Recipes { get; set; }
}
