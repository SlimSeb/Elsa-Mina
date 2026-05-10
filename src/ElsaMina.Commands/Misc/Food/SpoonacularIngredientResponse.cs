using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularIngredientResponse
{
    [JsonProperty("ingredients")]
    public List<SpoonacularIngredient> Ingredients { get; set; }
}
