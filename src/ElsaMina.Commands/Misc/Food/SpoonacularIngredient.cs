using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularIngredient
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}
