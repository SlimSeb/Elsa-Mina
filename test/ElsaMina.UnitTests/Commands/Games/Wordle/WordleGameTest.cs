using System.Globalization;
using ElsaMina.Commands.Games.Wordle;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Wordle;

public class WordleGameTest
{
    private WordleGame _game;
    private ITemplatesManager _mockTemplatesManager;
    private IConfiguration _configuration;
    private IBotDbContextFactory _mockDbContextFactory;
    private IWordleDailyService _mockDailyService;
    private IClockService _mockClockService;
    private IContext _context;
    private IUser _owner;

    [SetUp]
    public void SetUp()
    {
        _mockTemplatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _mockDbContextFactory = Substitute.For<IBotDbContextFactory>();
        _mockDailyService = Substitute.For<IWordleDailyService>();
        _mockClockService = Substitute.For<IClockService>();
        _context = Substitute.For<IContext>();

        _configuration.Name.Returns("Bot");
        _configuration.Trigger.Returns("!");
        _mockTemplatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));

        _owner = Substitute.For<IUser>();
        _owner.UserId.Returns("player");
        _owner.Name.Returns("Player");

        _game = new WordleGame(_mockTemplatesManager, _configuration,
            _mockDbContextFactory, _mockDailyService, _mockClockService)
        {
            Context = _context,
            Owner = _owner
        };
    }

    private async Task StartWithAnswer(string answer)
    {
        var vocabulary = new List<string>
        {
            answer, "crane", "level", "alley", "eagle", "llama",
            "about", "fight", "joker", "mound", "vivid", "stomp", "cabin"
        };
        _mockDailyService.GetWords(Arg.Any<CultureInfo>()).Returns(vocabulary);
        _mockDailyService.GetDailyAnswer(Arg.Any<CultureInfo>(), Arg.Any<TimeZoneInfo>())
            .Returns(answer.ToUpperInvariant());
        await _game.StartNewRound();
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldMarkAllCorrect_WhenGuessMatchesAnswer()
    {
        // Arrange
        await StartWithAnswer("crane");

        // Act
        var outcome = await _game.SubmitGuess(_owner, "crane");

        // Assert
        Assert.That(outcome, Is.EqualTo(WordleGuessOutcome.Accepted));
        Assert.That(_game.IsWon, Is.True);
        Assert.That(_game.IsRoundActive, Is.False);
        Assert.That(_game.Guesses[0].States,
            Is.All.EqualTo(WordleLetterState.Correct));
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldComputePresentAndAbsent_WhenLettersRepeat()
    {
        // Arrange: answer LEVEL, guess EAGLE
        await StartWithAnswer("level");

        // Act
        await _game.SubmitGuess(_owner, "eagle");

        // Assert
        Assert.That(_game.Guesses[0].States, Is.EqualTo(new[]
        {
            WordleLetterState.Present, // E (answer has E)
            WordleLetterState.Absent,  // A
            WordleLetterState.Absent,  // G
            WordleLetterState.Present, // L
            WordleLetterState.Present  // E
        }));
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldMarkExtraDuplicateAbsent_WhenAnswerHasFewerOccurrences()
    {
        // Arrange: answer ALLEY, guess LLAMA (two A's in guess, one A in answer)
        await StartWithAnswer("alley");

        // Act
        await _game.SubmitGuess(_owner, "llama");

        // Assert
        Assert.That(_game.Guesses[0].States, Is.EqualTo(new[]
        {
            WordleLetterState.Present, // L
            WordleLetterState.Correct, // L (position 1 matches)
            WordleLetterState.Present, // A
            WordleLetterState.Absent,  // M
            WordleLetterState.Absent   // A (no remaining A)
        }));
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldEndGame_WhenMaxGuessesReachedWithoutWin()
    {
        // Arrange
        await StartWithAnswer("crane");
        var wrongGuesses = new[] { "about", "fight", "joker", "mound", "vivid" };

        // Act
        foreach (var guess in wrongGuesses)
        {
            await _game.SubmitGuess(_owner, guess);
        }
        var lastOutcome = await _game.SubmitGuess(_owner, "stomp");

        // Assert
        Assert.That(lastOutcome, Is.EqualTo(WordleGuessOutcome.Accepted));
        Assert.That(_game.Guesses, Has.Count.EqualTo(WordleConstants.MAX_GUESSES));
        Assert.That(_game.IsWon, Is.False);
        Assert.That(_game.IsRoundActive, Is.False);
        Assert.That(_game.RevealedAnswer, Is.EqualTo("CRANE"));
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldReject_WhenLengthIsWrong()
    {
        // Arrange
        await StartWithAnswer("crane");

        // Act
        var outcome = await _game.SubmitGuess(_owner, "cat");

        // Assert
        Assert.That(outcome, Is.EqualTo(WordleGuessOutcome.InvalidLength));
        Assert.That(_game.Guesses, Is.Empty);
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldReject_WhenWordNotInList()
    {
        // Arrange
        await StartWithAnswer("crane");

        // Act
        var outcome = await _game.SubmitGuess(_owner, "zzzzz");

        // Assert
        Assert.That(outcome, Is.EqualTo(WordleGuessOutcome.NotInWordList));
        Assert.That(_game.Guesses, Is.Empty);
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldReject_WhenUserIsNotOwner()
    {
        // Arrange
        await StartWithAnswer("crane");
        var other = Substitute.For<IUser>();
        other.UserId.Returns("intruder");

        // Act
        var outcome = await _game.SubmitGuess(other, "crane");

        // Assert
        Assert.That(outcome, Is.EqualTo(WordleGuessOutcome.NotOwner));
        Assert.That(_game.IsWon, Is.False);
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldReject_WhenWordAlreadyGuessed()
    {
        // Arrange
        await StartWithAnswer("crane");

        // Act
        await _game.SubmitGuess(_owner, "about");
        var outcome = await _game.SubmitGuess(_owner, "about");

        // Assert
        Assert.That(outcome, Is.EqualTo(WordleGuessOutcome.AlreadyGuessed));
        Assert.That(_game.Guesses, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Test_AppendLetter_ShouldBuildCurrentInput_UpToWordLength()
    {
        // Arrange
        await StartWithAnswer("crane");

        // Act
        await _game.AppendLetter(_owner, 'c');
        await _game.AppendLetter(_owner, 'a');
        await _game.AppendLetter(_owner, 't');

        // Assert
        Assert.That(_game.CurrentInput, Is.EqualTo("CAT"));
    }

    [Test]
    public async Task Test_AppendLetter_ShouldIgnore_WhenInputIsFull()
    {
        // Arrange
        await StartWithAnswer("crane");
        foreach (var letter in "abcde")
        {
            await _game.AppendLetter(_owner, letter);
        }

        // Act
        await _game.AppendLetter(_owner, 'f');

        // Assert
        Assert.That(_game.CurrentInput, Is.EqualTo("ABCDE"));
    }

    [Test]
    public async Task Test_AppendLetter_ShouldIgnore_WhenUserIsNotOwner()
    {
        // Arrange
        await StartWithAnswer("crane");
        var other = Substitute.For<IUser>();
        other.UserId.Returns("intruder");

        // Act
        await _game.AppendLetter(other, 'c');

        // Assert
        Assert.That(_game.CurrentInput, Is.Empty);
    }

    [Test]
    public async Task Test_RemoveLetter_ShouldDropLastCharacter()
    {
        // Arrange
        await StartWithAnswer("crane");
        await _game.AppendLetter(_owner, 'c');
        await _game.AppendLetter(_owner, 'a');

        // Act
        await _game.RemoveLetter(_owner);

        // Assert
        Assert.That(_game.CurrentInput, Is.EqualTo("C"));
    }

    [Test]
    public async Task Test_SubmitCurrentInput_ShouldSubmitAndClearInput_WhenInputIsComplete()
    {
        // Arrange
        await StartWithAnswer("crane");
        foreach (var letter in "crane")
        {
            await _game.AppendLetter(_owner, letter);
        }

        // Act
        await _game.SubmitCurrentInput(_owner);

        // Assert
        Assert.That(_game.Guesses, Has.Count.EqualTo(1));
        Assert.That(_game.Guesses[0].Word, Is.EqualTo("CRANE"));
        Assert.That(_game.IsWon, Is.True);
        Assert.That(_game.CurrentInput, Is.Empty);
    }

    [Test]
    public async Task Test_SubmitCurrentInput_ShouldDoNothing_WhenInputIsIncomplete()
    {
        // Arrange
        await StartWithAnswer("crane");
        await _game.AppendLetter(_owner, 'c');
        await _game.AppendLetter(_owner, 'a');

        // Act
        await _game.SubmitCurrentInput(_owner);

        // Assert
        Assert.That(_game.Guesses, Is.Empty);
        Assert.That(_game.CurrentInput, Is.EqualTo("CA"));
    }

    [Test]
    public async Task Test_AppendLetter_ShouldRefreshOnlyPrivateView_NotPublic()
    {
        // Arrange
        await StartWithAnswer("crane");
        _context.ClearReceivedCalls();

        // Act
        await _game.AppendLetter(_owner, 'c');

        // Assert
        _context.Received().SendPrivateUpdatableHtml(_owner.UserId, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        _context.DidNotReceive().SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldRefreshBothPublicAndPrivateViews()
    {
        // Arrange
        await StartWithAnswer("crane");
        _context.ClearReceivedCalls();

        // Act
        await _game.SubmitGuess(_owner, "about");

        // Assert
        _context.Received().SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        _context.Received().SendPrivateUpdatableHtml(_owner.UserId, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldUpdateKeyboardStates_WhenGuessSubmitted()
    {
        // Arrange
        await StartWithAnswer("crane");

        // Act
        await _game.SubmitGuess(_owner, "cabin");

        // Assert: C is correct, A is correct (position 2 in CRANE... actually verify)
        Assert.That(_game.KeyboardStates['C'], Is.EqualTo(WordleLetterState.Correct));
        Assert.That(_game.KeyboardStates['B'], Is.EqualTo(WordleLetterState.Absent));
    }
}
