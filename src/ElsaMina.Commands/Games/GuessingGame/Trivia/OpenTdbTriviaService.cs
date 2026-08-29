using System.Net;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Games.GuessingGame.Trivia;

public class OpenTdbTriviaService : ITriviaService
{
    private const string API_URL = "https://opentdb.com/api.php?amount={0}";

    private static readonly IReadOnlyList<string> TRUE_SYNONYMS =
        ["True", "T", "Vrai", "V", "Verdadero", "Vero", "Wahr", "1"];

    private static readonly IReadOnlyList<string> FALSE_SYNONYMS =
        ["False", "F", "Faux", "Falso", "Falsch", "0"];

    private readonly IHttpService _httpService;
    private readonly IRandomService _randomService;

    public OpenTdbTriviaService(IHttpService httpService, IRandomService randomService)
    {
        _httpService = httpService;
        _randomService = randomService;
    }

    public async Task<IReadOnlyList<TriviaQuestion>> GetQuestionsAsync(int amount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = HttpRequest.Get(string.Format(API_URL, amount));
            var response = await _httpService.SendAsync<OpenTdbResponse>(request, cancellationToken);

            if (response.Data == null || response.Data.ResponseCode != 0 || response.Data.Results == null)
            {
                Log.Warning("OpenTDB returned response code: {0}", response.Data?.ResponseCode);
                return Array.Empty<TriviaQuestion>();
            }

            var questions = new List<TriviaQuestion>();
            foreach (var dto in response.Data.Results)
            {
                var question = MapToTriviaQuestion(dto);
                if (question != null)
                {
                    questions.Add(question);
                }
            }

            return questions;
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to fetch trivia questions from OpenTDB");
            return Array.Empty<TriviaQuestion>();
        }
    }

    private TriviaQuestion MapToTriviaQuestion(OpenTdbQuestionDto dto)
    {
        var questionText = WebUtility.HtmlDecode(dto.Question);
        var category = WebUtility.HtmlDecode(dto.Category);
        var difficulty = WebUtility.HtmlDecode(dto.Difficulty);
        var correctAnswer = WebUtility.HtmlDecode(dto.CorrectAnswer);
        var incorrectAnswers = dto.IncorrectAnswers?
            .Select(WebUtility.HtmlDecode)
            .ToList() ?? [];

        var isBoolean = string.Equals(dto.Type, "boolean", StringComparison.OrdinalIgnoreCase);

        if (isBoolean)
        {
            var isTrue = string.Equals(correctAnswer, "True", StringComparison.OrdinalIgnoreCase);
            var normalizedCorrectAnswer = isTrue ? "True" : "False";
            var validAnswers = isTrue ? TRUE_SYNONYMS : FALSE_SYNONYMS;

            return new TriviaQuestion
            {
                Question = questionText,
                Category = category,
                Difficulty = difficulty,
                Type = TriviaQuestionType.Boolean,
                CorrectAnswer = normalizedCorrectAnswer,
                Options = ["True", "False"],
                ValidAnswers = validAnswers
            };
        }

        var options = new List<string> { correctAnswer };
        options.AddRange(incorrectAnswers);
        _randomService.ShuffleInPlace(options);

        var correctIndex = options.IndexOf(correctAnswer);
        var validAnswersList = new List<string> { correctAnswer };

        if (correctIndex >= 0)
        {
            validAnswersList.Add(((char)('A' + correctIndex)).ToString());
            validAnswersList.Add((correctIndex + 1).ToString());
        }

        return new TriviaQuestion
        {
            Question = questionText,
            Category = category,
            Difficulty = difficulty,
            Type = TriviaQuestionType.Multiple,
            CorrectAnswer = correctAnswer,
            Options = options,
            ValidAnswers = validAnswersList
        };
    }
}
