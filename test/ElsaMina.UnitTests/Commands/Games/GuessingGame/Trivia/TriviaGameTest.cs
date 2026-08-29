using System.Globalization;
using ElsaMina.Commands.Games.GuessingGame.Trivia;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.GuessingGame.Trivia;

[TestFixture]
public class TriviaGameTest
{
    private ITriviaService _triviaService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IClockService _clockService;
    private IContext _context;
    private TestTriviaGame _game;

    [SetUp]
    public void SetUp()
    {
        _triviaService = Substitute.For<ITriviaService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _clockService = Substitute.For<IClockService>();
        _context = Substitute.For<IContext>();

        _configuration.Name.Returns("ElsaBot");
        _configuration.Trigger.Returns("-");
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _context.RoomId.Returns("testroom");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult("<template/>"));

        _game = new TestTriviaGame(_triviaService, _templatesManager, _configuration, _clockService)
        {
            Context = _context,
            TurnsCount = 2
        };
    }

    [Test]
    public void Test_OnGameStart_ShouldReplyTriviaStart()
    {
        // Act
        _game.CallOnGameStart();

        // Assert
        _context.Received(1).ReplyLocalizedMessage("trivia_start");
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldUseMultipleChoiceTemplate_WhenQuestionIsMultiple()
    {
        // Arrange
        var question = new TriviaQuestion
        {
            Question = "What is 2+2?",
            Category = "Math",
            Difficulty = "easy",
            Type = TriviaQuestionType.Multiple,
            CorrectAnswer = "4",
            Options = ["1", "2", "3", "4"],
            ValidAnswers = ["4", "D", "4"]
        };

        _triviaService.GetQuestionsAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TriviaQuestion>>([question, question]));

        // Act
        await _game.CallOnTurnStart();

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Games/GuessingGame/Trivia/TriviaMultipleChoicePanel",
            Arg.Any<TriviaGamePanelViewModel>());
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), "<template/>", false);
        Assert.That(_game.ValidAnswers, Does.Contain("4"));
        Assert.That(_game.ValidAnswers, Does.Contain("D"));
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldUseBooleanTemplate_WhenQuestionIsBoolean()
    {
        // Arrange
        var question = new TriviaQuestion
        {
            Question = "The earth is flat.",
            Category = "Science",
            Difficulty = "easy",
            Type = TriviaQuestionType.Boolean,
            CorrectAnswer = "False",
            Options = ["True", "False"],
            ValidAnswers = ["False", "F", "Faux"]
        };

        _triviaService.GetQuestionsAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TriviaQuestion>>([question, question]));

        // Act
        await _game.CallOnTurnStart();

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Games/GuessingGame/Trivia/TriviaBooleanPanel",
            Arg.Any<TriviaGamePanelViewModel>());
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), "<template/>", false);
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldReplyErrorAndStopGame_WhenQuestionsEmpty()
    {
        // Arrange
        _triviaService.GetQuestionsAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TriviaQuestion>>([]));

        // Act
        await _game.CallOnTurnStart();

        // Assert
        _context.Received(1).ReplyLocalizedMessage("trivia_fetch_error");
        Assert.That(_game.IsEnded, Is.True);
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldAdjustTurnsCount_WhenFewerQuestionsReturned()
    {
        // Arrange
        _game.TurnsCount = 5;
        var question = new TriviaQuestion
        {
            Question = "Question 1",
            Category = "General",
            Difficulty = "easy",
            Type = TriviaQuestionType.Multiple,
            CorrectAnswer = "Answer",
            Options = ["Answer"],
            ValidAnswers = ["Answer"]
        };

        _triviaService.GetQuestionsAsync(5, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TriviaQuestion>>([question]));

        // Act
        await _game.CallOnTurnStart();

        // Assert
        Assert.That(_game.TurnsCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Test_OnCorrectAnswer_ShouldRevealAnswer()
    {
        // Arrange
        var question = new TriviaQuestion
        {
            Question = "What is 2+2?",
            Category = "Math",
            Difficulty = "easy",
            Type = TriviaQuestionType.Multiple,
            CorrectAnswer = "4",
            Options = ["4"],
            ValidAnswers = ["4"]
        };

        _triviaService.GetQuestionsAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TriviaQuestion>>([question, question]));

        await _game.CallOnTurnStart();

        // Act
        _game.CallOnCorrectAnswer();

        // Assert
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), "<template/>", true);
    }

    [Test]
    public async Task Test_OnTimerCountdown_ShouldRevealAnswer_WhenRemainingTimeIsZero()
    {
        // Arrange
        var question = new TriviaQuestion
        {
            Question = "What is 2+2?",
            Category = "Math",
            Difficulty = "easy",
            Type = TriviaQuestionType.Multiple,
            CorrectAnswer = "4",
            Options = ["4"],
            ValidAnswers = ["4"]
        };

        _triviaService.GetQuestionsAsync(2, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TriviaQuestion>>([question, question]));

        await _game.CallOnTurnStart();

        // Act
        _game.CallOnTimerCountdown(TimeSpan.Zero);

        // Assert
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), "<template/>", true);
    }

    private class TestTriviaGame : TriviaGame
    {
        public TestTriviaGame(ITriviaService triviaService,
            ITemplatesManager templatesManager,
            IConfiguration configuration,
            IClockService clockService)
            : base(triviaService, templatesManager, configuration, clockService)
        {
        }

        public IEnumerable<string> ValidAnswers => CurrentValidAnswers;

        public void CallOnGameStart() => OnGameStart();
        public Task CallOnTurnStart() => OnTurnStart();
        public void CallOnCorrectAnswer() => OnCorrectAnswer();
        public void CallOnTimerCountdown(TimeSpan remainingTime) => OnTimerCountdown(remainingTime);
    }
}
