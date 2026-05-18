using ElsaMina.Commands.Misc;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc;

public class PairingsCommandTest
{
    private IHttpService _httpService;
    private IRandomService _randomService;
    private IContext _context;
    private PairingsCommand _command;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _randomService = Substitute.For<IRandomService>();
        _context = Substitute.For<IContext>();
        _command = new PairingsCommand(_httpService, _randomService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        // Assert
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        // Assert
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleCommaSeparatedPlayers()
    {
        // Arrange
        _context.Target.Returns("Player1, Player2, Player3, Player4");
        var players = new List<string> { "Player1", "Player2", "Player3", "Player4" };
        _randomService.When(x => x.ShuffleInPlace(Arg.Any<List<string>>()))
            .Do(x => 
            {
                var list = (List<string>)x[0];
                // Don't clear the list, just verify it has the right contents
                Assert.That(list, Has.Count.EqualTo(4));
                Assert.That(list, Does.Contain("Player1"));
                Assert.That(list, Does.Contain("Player2"));
                Assert.That(list, Does.Contain("Player3"));
                Assert.That(list, Does.Contain("Player4"));
            });

        // Act
        await _command.RunAsync(_context);

        // Assert
        _randomService.Received(1).ShuffleInPlace(Arg.Any<List<string>>());
        _context.Received(1).ReplyHtml(Arg.Is<string>(s => 
            s.Contains(" vs. ") && 
            s.Contains("Player1") && 
            s.Contains("Player2") && 
            s.Contains("Player3") && 
            s.Contains("Player4")), 
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandlePastebinUrl()
    {
        // Arrange
        _context.Target.Returns("https://pastebin.com/abc123");
        _httpService.GetAsync<string>(
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, string>>(),
            Arg.Any<IDictionary<string, string>>(),
            Arg.Any<bool>(),
            true,
            Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<string> { Data = "Player1\nPlayer2\nPlayer3\nPlayer4" });
        var players = new List<string> { "Player1", "Player2", "Player3", "Player4" };
        _randomService.When(x => x.ShuffleInPlace(Arg.Any<List<string>>()))
            .Do(x => 
            {
                var list = (List<string>)x[0];
                Assert.That(list, Has.Count.EqualTo(4));
                Assert.That(list, Does.Contain("Player1"));
                Assert.That(list, Does.Contain("Player2"));
                Assert.That(list, Does.Contain("Player3"));
                Assert.That(list, Does.Contain("Player4"));
            });

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _httpService.Received(1).GetAsync<string>(
            Arg.Is<string>(s => s.StartsWith("https://pastebin.com/raw/")), 
            Arg.Any<IDictionary<string, string>>(),
            Arg.Any<IDictionary<string, string>>(),
            Arg.Any<bool>(),
            true, 
            Arg.Any<CancellationToken>());
        _randomService.Received(1).ShuffleInPlace(Arg.Any<List<string>>());
        _context.Received(1).ReplyHtml(Arg.Is<string>(s => 
            s.Contains(" vs. ") && 
            s.Contains("Player1") && 
            s.Contains("Player2") && 
            s.Contains("Player3") && 
            s.Contains("Player4")), 
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleOddNumberOfPlayers()
    {
        // Arrange
        _context.Target.Returns("Player1, Player2, Player3");
        var players = new List<string> { "Player1", "Player2", "Player3" };
        _randomService.When(x => x.ShuffleInPlace(Arg.Any<List<string>>()))
            .Do(x => 
            {
                var list = (List<string>)x[0];
                Assert.That(list, Has.Count.EqualTo(3));
                Assert.That(list, Does.Contain("Player1"));
                Assert.That(list, Does.Contain("Player2"));
                Assert.That(list, Does.Contain("Player3"));
            });

        // Act
        await _command.RunAsync(_context);

        // Assert
        _randomService.Received(1).ShuffleInPlace(Arg.Any<List<string>>());
        _context.Received(1).ReplyHtml(Arg.Is<string>(s => 
            s.Contains(" vs. ") && 
            s.Contains("Player1") && 
            s.Contains("Player2") && 
            s.Contains("Player3") && 
            s.Contains("Bye #1")), 
            rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleEmptyInput()
    {
        // Arrange
        _context.Target.Returns("");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _randomService.DidNotReceive().ShuffleInPlace(Arg.Any<List<string>>());
        _context.Received(1).ReplyHtml(Arg.Is<string>(s => s == ""), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleWhitespaceOnlyInput()
    {
        // Arrange
        _context.Target.Returns("   ,  ,  ");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _randomService.DidNotReceive().ShuffleInPlace(Arg.Any<List<string>>());
        _context.Received(1).ReplyHtml(Arg.Is<string>(s => s == ""), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleHttpError()
    {
        // Arrange
        _context.Target.Returns("https://pastebin.com/abc123");
        _httpService.GetAsync<string>(
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, string>>(),
            Arg.Any<IDictionary<string, string>>(),
            Arg.Any<bool>(),
            true,
            Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<string> { Data = "" });

        // Act
        await _command.RunAsync(_context);

        // Assert
        _randomService.DidNotReceive().ShuffleInPlace(Arg.Any<List<string>>());
        _context.Received(1).ReplyHtml(Arg.Is<string>(s => s == ""), rankAware: true);
    }
} 