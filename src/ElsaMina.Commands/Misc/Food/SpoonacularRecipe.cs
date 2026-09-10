using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularRecipe
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; }
}
