using Newtonsoft.Json;

namespace ElsaMina.Commands.Games.GuessingGame.Trivia;

public class OpenTdbQuestionDto
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("difficulty")]
    public string Difficulty { get; set; }

    [JsonProperty("category")]
    public string Category { get; set; }

    [JsonProperty("question")]
    public string Question { get; set; }

    [JsonProperty("correct_answer")]
    public string CorrectAnswer { get; set; }

    [JsonProperty("incorrect_answers")]
    public List<string> IncorrectAnswers { get; set; }
}
