using System.Net;
using ElsaMina.Commands.Games.GuessingGame.Trivia;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Probabilities;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Games.GuessingGame.Trivia;

[TestFixture]
public class OpenTdbTriviaServiceTest
{
    private IHttpService _httpService;
    private IRandomService _randomService;
    private OpenTdbTriviaService _service;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _randomService = Substitute.For<IRandomService>();
        _service = new OpenTdbTriviaService(_httpService, _randomService);
    }

    [Test]
    public async Task Test_GetQuestionsAsync_ShouldReturnDecodedQuestions_WhenApiResponseIsSuccessful()
    {
        // Arrange
        var response = new OpenTdbResponse
        {
            ResponseCode = 0,
            Results =
            [
                new OpenTdbQuestionDto
                {
                    Type = "multiple",
                    Difficulty = "medium",
                    Category = "Science &amp; Nature",
                    Question = "What snowy mob was added in Minecraft 1.10?",
                    CorrectAnswer = "Polar &quot;bears&quot;",
                    IncorrectAnswers = ["Eskimos", "Penguins", "Walking TNT"]
                }
            ]
        };

        var httpResponse = Substitute.For<IHttpResponse<OpenTdbResponse>>();
        httpResponse.Data.Returns(response);

        _httpService.SendAsync<OpenTdbResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(httpResponse);

        // Act
        var result = await _service.GetQuestionsAsync(1);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        var question = result[0];
        Assert.That(question.Category, Is.EqualTo("Science & Nature"));
        Assert.That(question.Question, Is.EqualTo("What snowy mob was added in Minecraft 1.10?"));
        Assert.That(question.CorrectAnswer, Is.EqualTo("Polar \"bears\""));
        Assert.That(question.Type, Is.EqualTo(TriviaQuestionType.Multiple));
        Assert.That(question.Options, Has.Count.EqualTo(4));
        Assert.That(question.ValidAnswers, Does.Contain("Polar \"bears\""));
    }

    [Test]
    public async Task Test_GetQuestionsAsync_ShouldHandleBooleanQuestion_WhenTypeIsBoolean()
    {
        // Arrange
        var response = new OpenTdbResponse
        {
            ResponseCode = 0,
            Results =
            [
                new OpenTdbQuestionDto
                {
                    Type = "boolean",
                    Difficulty = "easy",
                    Category = "General Knowledge",
                    Question = "The sky is &quot;blue&quot;.",
                    CorrectAnswer = "True",
                    IncorrectAnswers = ["False"]
                }
            ]
        };

        var httpResponse = Substitute.For<IHttpResponse<OpenTdbResponse>>();
        httpResponse.Data.Returns(response);

        _httpService.SendAsync<OpenTdbResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(httpResponse);

        // Act
        var result = await _service.GetQuestionsAsync(1);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        var question = result[0];
        Assert.That(question.Type, Is.EqualTo(TriviaQuestionType.Boolean));
        Assert.That(question.CorrectAnswer, Is.EqualTo("True"));
        Assert.That(question.Options, Is.EqualTo(new[] { "True", "False" }));
        Assert.That(question.ValidAnswers, Does.Contain("True"));
        Assert.That(question.ValidAnswers, Does.Contain("Vrai"));
    }

    [Test]
    public async Task Test_GetQuestionsAsync_ShouldReturnEmptyList_WhenResponseCodeIsNotZero()
    {
        // Arrange
        var response = new OpenTdbResponse
        {
            ResponseCode = 5,
            Results = null
        };

        var httpResponse = Substitute.For<IHttpResponse<OpenTdbResponse>>();
        httpResponse.Data.Returns(response);

        _httpService.SendAsync<OpenTdbResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(httpResponse);

        // Act
        var result = await _service.GetQuestionsAsync(1);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Test_GetQuestionsAsync_ShouldReturnEmptyList_WhenHttpServiceThrows()
    {
        // Arrange
        _httpService.SendAsync<OpenTdbResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _service.GetQuestionsAsync(1);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task Test_GetQuestionsAsync_ShouldIncludeOptionLetterAndIndexInValidAnswers_ForMultipleChoice()
    {
        // Arrange
        var response = new OpenTdbResponse
        {
            ResponseCode = 0,
            Results =
            [
                new OpenTdbQuestionDto
                {
                    Type = "multiple",
                    Difficulty = "easy",
                    Category = "Science",
                    Question = "What is H2O?",
                    CorrectAnswer = "Water",
                    IncorrectAnswers = ["Fire", "Earth", "Air"]
                }
            ]
        };

        var httpResponse = Substitute.For<IHttpResponse<OpenTdbResponse>>();
        httpResponse.Data.Returns(response);

        _httpService.SendAsync<OpenTdbResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(httpResponse);

        // Act
        var result = await _service.GetQuestionsAsync(1);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        var question = result[0];
        var correctIndex = ((List<string>)question.Options).IndexOf("Water");
        var expectedLetter = ((char)('A' + correctIndex)).ToString();
        var expectedNumber = (correctIndex + 1).ToString();

        Assert.That(question.ValidAnswers, Does.Contain("Water"));
        Assert.That(question.ValidAnswers, Does.Contain(expectedLetter));
        Assert.That(question.ValidAnswers, Does.Contain(expectedNumber));
    }
}
