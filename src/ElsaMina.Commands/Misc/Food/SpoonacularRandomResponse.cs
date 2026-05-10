using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularRandomResponse
{
    [JsonProperty("recipes")]
    public List<SpoonacularRecipe> Recipes { get; set; }
}
