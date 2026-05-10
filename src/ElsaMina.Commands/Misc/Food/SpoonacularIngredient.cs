using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularIngredient
{
    [JsonProperty("name")]
    public string Name { get; set; }
}
