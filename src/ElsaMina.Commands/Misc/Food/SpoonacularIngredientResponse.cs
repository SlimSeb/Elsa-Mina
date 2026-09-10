using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularIngredientResponse
{
    [JsonPropertyName("ingredients")]
    public List<SpoonacularIngredient> Ingredients { get; set; }
}
