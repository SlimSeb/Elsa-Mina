using ElsaMina.Commands.Games.Semantix;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Semantix;

public class SemantixGameTest
{
    private static readonly DateOnly TODAY = new(2026, 6, 15);

    private IEmbeddingService _embeddingService;
    private ISemantixDailyService _dailyService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private DbContextOptions<BotDbContext> _dbOptions;
    private IContext _context;
    private IUser _owner;
    private SemantixGame _game;

    [SetUp]
    public void SetUp()
    {
        _embeddingService = Substitute.For<IEmbeddingService>();
        _dailyService = Substitute.For<ISemantixDailyService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();

        _dailyService.GetDailyAnswer().Returns("pomme");
        _dailyService.IsValidWord(Arg.Any<string>()).Returns(true);
        _dailyService.Today.Returns(TODAY);
        _embeddingService.GetEmbeddingAsync("pomme", Arg.Any<CancellationToken>()).Returns([1f, 0f]);
        _embeddingService.GetEmbeddingAsync("poire", Arg.Any<CancellationToken>()).Returns([1f, 1f]);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult("<p>\nboard\n</p>"));

        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContextFactory = Substitute.For<IBotDbContextFactory>();
        dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("arcade");
        _owner = Substitute.For<IUser>();
        _owner.UserId.Returns("player");
        _owner.Name.Returns("Player");

        _game = new SemantixGame(_embeddingService, _dailyService, _templatesManager, _configuration,
            dbContextFactory)
        {
            Context = _context,
            Owner = _owner
        };
    }

    [TearDown]
    public async Task TearDown()
    {
        // Stops the inactivity timer
        await _game.CancelAsync();
    }

    private static IUser CreateUser(string userId)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(userId);
        return user;
    }

    private async Task<SemantixScore> GetScoreAsync()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        return await dbContext.SemantixScores.FindAsync("player");
    }

    [Test]
    public void Test_Identifier_ShouldReturnSemantixGame()
    {
        Assert.That(_game.Identifier, Is.EqualTo("SemantixGame"));
    }

    [Test]
    public async Task Test_StartNewRound_ShouldStartRoundAndRenderBoards()
    {
        // Act
        var started = await _game.StartNewRound();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(started, Is.True);
            Assert.That(_game.IsRoundActive, Is.True);
            Assert.That(_game.IsStarted, Is.True);
            Assert.That(_game.Answer, Is.EqualTo("pomme"));
            Assert.That(_game.Guesses, Is.Empty);
        }
        _context.Received(1).ReplyLocalizedMessage("sx_game_started");
        _context.Received(1).SendUpdatableHtml(Arg.Is<string>(id => id.StartsWith("sx-pub-arcade-")),
            "<p>board</p>", false);
        _context.Received(1).SendHtmlPageTo("player", "semantix-arcade", "<p>board</p>");
    }

    [Test]
    public async Task Test_StartNewRound_ShouldOnlyRenderPrivateBoard_WhenInPrivateMode()
    {
        // Arrange
        _game.IsPrivateMode = true;
        _game.TargetRoomId = "casino";

        // Act
        await _game.StartNewRound();

        // Assert
        _context.DidNotReceive().SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        _context.Received(1).SendHtmlPageTo("player", "semantix-casino", Arg.Any<string>());
    }

    [Test]
    public async Task Test_StartNewRound_ShouldFail_WhenDailyAnswerIsUnavailable()
    {
        // Arrange
        _dailyService.GetDailyAnswer().Returns((string)null);

        // Act
        var started = await _game.StartNewRound();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(started, Is.False);
            Assert.That(_game.IsRoundActive, Is.False);
        }
        _context.Received(1).ReplyLocalizedMessage("sx_game_unavailable");
    }

    [Test]
    public async Task Test_StartNewRound_ShouldFail_WhenAnswerEmbeddingIsUnavailable()
    {
        // Arrange
        _embeddingService.GetEmbeddingAsync("pomme", Arg.Any<CancellationToken>()).Returns((float[])null);

        // Act
        var started = await _game.StartNewRound();

        // Assert
        Assert.That(started, Is.False);
        _context.Received(1).ReplyLocalizedMessage("sx_game_api_unavailable");
    }

    [Test]
    public async Task Test_ResumeAsync_ShouldRenderBoardsAgain()
    {
        // Arrange
        await _game.StartNewRound();
        _context.ClearReceivedCalls();

        // Act
        await _game.ResumeAsync();

        // Assert
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), true);
        _context.Received(1).SendHtmlPageTo("player", "semantix-arcade", Arg.Any<string>());
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldReturnRoundNotActive_WhenRoundIsNotStarted()
    {
        // Act
        var outcome = await _game.SubmitGuess(_owner, "poire");

        // Assert
        Assert.That(outcome, Is.EqualTo(SemantixGuessOutcome.RoundNotActive));
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldReturnNotOwner_WhenAnotherUserGuesses()
    {
        // Arrange
        await _game.StartNewRound();

        // Act
        var outcome = await _game.SubmitGuess(CreateUser("other"), "poire");

        // Assert
        Assert.That(outcome, Is.EqualTo(SemantixGuessOutcome.NotOwner));
    }

    [TestCase(null)]
    [TestCase("   ")]
    public async Task Test_SubmitGuess_ShouldRejectEmptyWord(string word)
    {
        // Arrange
        await _game.StartNewRound();
        _context.ClearReceivedCalls();

        // Act
        var outcome = await _game.SubmitGuess(_owner, word);

        // Assert
        Assert.That(outcome, Is.EqualTo(SemantixGuessOutcome.EmptyWord));
        _context.Received(1).SendHtmlPageTo("player", Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldRejectWord_WhenNotInWordList()
    {
        // Arrange
        await _game.StartNewRound();
        _dailyService.IsValidWord("zzz").Returns(false);

        // Act
        var outcome = await _game.SubmitGuess(_owner, "zzz");

        // Assert
        Assert.That(outcome, Is.EqualTo(SemantixGuessOutcome.NotInWordList));
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldAcceptGuessWithTemperature_WhenWordIsValid()
    {
        // Arrange
        await _game.StartNewRound();

        // Act
        var outcome = await _game.SubmitGuess(_owner, "  Poire ");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(SemantixGuessOutcome.Accepted));
            Assert.That(_game.Guesses, Has.Count.EqualTo(1));
            Assert.That(_game.LastGuess.Word, Is.EqualTo("poire"));
            Assert.That(_game.LastGuess.Order, Is.EqualTo(1));
            Assert.That(_game.LastGuess.Similarity, Is.EqualTo(Math.Sqrt(0.5)).Within(0.0001));
            Assert.That(_game.LastGuess.Temperature, Is.EqualTo(SemantixConstants.MAX_TEMPERATURE));
            Assert.That(_game.IsRoundActive, Is.True);
        }
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldRejectWord_WhenAlreadyGuessed()
    {
        // Arrange
        await _game.StartNewRound();
        await _game.SubmitGuess(_owner, "poire");

        // Act
        var outcome = await _game.SubmitGuess(_owner, "POIRE");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(SemantixGuessOutcome.AlreadyGuessed));
            Assert.That(_game.Guesses, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldReturnEmbeddingUnavailable_WhenGuessHasNoEmbedding()
    {
        // Arrange
        await _game.StartNewRound();
        _embeddingService.GetEmbeddingAsync("chat", Arg.Any<CancellationToken>()).Returns((float[])null);

        // Act
        var outcome = await _game.SubmitGuess(_owner, "chat");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(SemantixGuessOutcome.EmbeddingUnavailable));
            Assert.That(_game.Guesses, Is.Empty);
        }
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldWinAndSaveScore_WhenAnswerIsFound()
    {
        // Arrange
        await _game.StartNewRound();
        await _game.SubmitGuess(_owner, "poire");
        var ended = false;
        _game.GameEnded += () => ended = true;

        // Act
        var outcome = await _game.SubmitGuess(_owner, "pomme");

        // Assert
        var score = await GetScoreAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(SemantixGuessOutcome.Won));
            Assert.That(_game.IsWon, Is.True);
            Assert.That(_game.IsRoundActive, Is.False);
            Assert.That(ended, Is.True);
            Assert.That(_game.LastGuess.Temperature, Is.EqualTo(SemantixConstants.WIN_TEMPERATURE));
            Assert.That(score.GamesPlayed, Is.EqualTo(1));
            Assert.That(score.Wins, Is.EqualTo(1));
            Assert.That(score.TotalGuesses, Is.EqualTo(2));
            Assert.That(score.BestGuessCount, Is.EqualTo(2));
            Assert.That(score.CurrentStreak, Is.EqualTo(1));
            Assert.That(score.MaxStreak, Is.EqualTo(1));
            Assert.That(score.LastWonDate, Is.EqualTo(TODAY));
        }
        _context.Received(1).ReplyLocalizedMessage("sx_game_win", "Player", 2);
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldExtendStreak_WhenPlayerWonYesterday()
    {
        // Arrange
        await using (var dbContext = new BotDbContext(_dbOptions))
        {
            await dbContext.EnsureUserExistsAsync("player");
            dbContext.SemantixScores.Add(new SemantixScore
            {
                UserId = "player",
                GamesPlayed = 3,
                Wins = 3,
                TotalGuesses = 30,
                BestGuessCount = 5,
                CurrentStreak = 3,
                MaxStreak = 3,
                LastWonDate = TODAY.AddDays(-1)
            });
            await dbContext.SaveChangesAsync();
        }
        await _game.StartNewRound();

        // Act
        await _game.SubmitGuess(_owner, "pomme");

        // Assert
        var score = await GetScoreAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(score.GamesPlayed, Is.EqualTo(4));
            Assert.That(score.TotalGuesses, Is.EqualTo(31));
            Assert.That(score.BestGuessCount, Is.EqualTo(1));
            Assert.That(score.CurrentStreak, Is.EqualTo(4));
            Assert.That(score.MaxStreak, Is.EqualTo(4));
        }
    }

    [Test]
    public async Task Test_SubmitGuess_ShouldResetStreak_WhenLastWinIsOlderThanYesterday()
    {
        // Arrange
        await using (var dbContext = new BotDbContext(_dbOptions))
        {
            await dbContext.EnsureUserExistsAsync("player");
            dbContext.SemantixScores.Add(new SemantixScore
            {
                UserId = "player",
                BestGuessCount = 1,
                CurrentStreak = 5,
                MaxStreak = 5,
                LastWonDate = TODAY.AddDays(-3)
            });
            await dbContext.SaveChangesAsync();
        }
        await _game.StartNewRound();
        await _game.SubmitGuess(_owner, "poire");

        // Act
        await _game.SubmitGuess(_owner, "pomme");

        // Assert
        var score = await GetScoreAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(score.CurrentStreak, Is.EqualTo(1));
            Assert.That(score.MaxStreak, Is.EqualTo(5));
            Assert.That(score.BestGuessCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_CancelAsync_ShouldEndRound()
    {
        // Arrange
        await _game.StartNewRound();

        // Act
        await _game.CancelAsync();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.IsRoundActive, Is.False);
            Assert.That(_game.IsEnded, Is.True);
        }
    }

    [Test]
    public async Task Test_CancelAsync_ShouldNotRender_WhenNobodyOwnsTheGame()
    {
        // Arrange
        _game.Owner = null;

        // Act
        await _game.CancelAsync();

        // Assert
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }
}
