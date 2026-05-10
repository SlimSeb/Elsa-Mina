using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularRecipe
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("image")]
    public string Image { get; set; }
}
