using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularSearchResponse
{
    [JsonProperty("results")]
    public List<SpoonacularRecipe> Results { get; set; }
}
