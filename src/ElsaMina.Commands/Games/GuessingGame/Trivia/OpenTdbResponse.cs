using Newtonsoft.Json;

namespace ElsaMina.Commands.Games.GuessingGame.Trivia;

public class OpenTdbResponse
{
    [JsonProperty("response_code")]
    public int ResponseCode { get; set; }

    [JsonProperty("results")]
    public List<OpenTdbQuestionDto> Results { get; set; }
}
