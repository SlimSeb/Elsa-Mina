using Newtonsoft.Json;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularStep
{
    [JsonProperty("step")]
    public string Step { get; set; }
}
