using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Games.GuessingGame.Trivia;

public class OpenTdbResponse
{
    [JsonPropertyName("response_code")]
    public int ResponseCode { get; set; }

    [JsonPropertyName("results")]
    public List<OpenTdbQuestionDto> Results { get; set; }
}
