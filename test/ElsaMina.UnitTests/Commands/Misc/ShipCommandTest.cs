using ElsaMina.Commands.Misc;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc;

[TestFixture]
public class ShipCommandTest
{
    private IContext _context;
    private ShipCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _command = new ShipCommand();
    }

    private (int Score, string Emoji) CaptureShipResult()
    {
        int capturedScore = -1;
        string capturedEmoji = null;
        _context.When(c => c.ReplyRankAwareLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>()))
            .Do(call =>
            {
                capturedScore = (int)call.ArgAt<object[]>(1)[2];
                capturedEmoji = (string)call.ArgAt<object[]>(1)[3];
            });
        return (capturedScore, capturedEmoji);
    }

    // Properties

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeShipHelp()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("ship_help"));
    }

    // Help guard

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetHasNoComma()
    {
        // Arrange
        _context.Target.Returns("Alice");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        _context.DidNotReceive().ReplyRankAwareLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenFirstNameIsEmpty()
    {
        // Arrange
        _context.Target.Returns(",Bob");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        _context.DidNotReceive().ReplyRankAwareLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenSecondNameIsEmpty()
    {
        // Arrange
        _context.Target.Returns("Alice,");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        _context.DidNotReceive().ReplyRankAwareLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenBothNamesAreWhitespace()
    {
        // Arrange
        _context.Target.Returns("  ,  ");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        _context.DidNotReceive().ReplyRankAwareLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    // Ship result

    [Test]
    public async Task Test_RunAsync_ShouldReplyShipResult_WhenBothNamesAreProvided()
    {
        // Arrange
        _context.Target.Returns("Alice,Bob");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("ship_result",
            "Alice", "Bob", Arg.Any<int>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldPreserveOriginalNameCasing_WhenNamesAreProvided()
    {
        // Arrange
        _context.Target.Returns("ALICE,BOB");

        // Act
        await _command.RunAsync(_context);

        // Assert - score computation lowercases internally but the reply gets the original-cased names
        _context.Received(1).ReplyRankAwareLocalizedMessage("ship_result",
            "ALICE", "BOB", Arg.Any<int>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimNames_WhenTargetHasExtraWhitespace()
    {
        // Arrange
        _context.Target.Returns("  Alice  ,  Bob  ");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("ship_result",
            "Alice", "Bob", Arg.Any<int>(), Arg.Any<string>());
    }

    // Score properties

    [Test]
    public async Task Test_RunAsync_ShouldProduceScoreInValidRange_WhenNamesAreProvided()
    {
        // Arrange
        _context.Target.Returns("Alice,Bob");
        var (_, _) = CaptureShipResult();
        int capturedScore = -1;
        _context.When(c => c.ReplyRankAwareLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>()))
            .Do(call => capturedScore = (int)call.ArgAt<object[]>(1)[2]);

        // Act
        await _command.RunAsync(_context);

        // Assert
        Assert.That(capturedScore, Is.InRange(0, 100));
    }

    [Test]
    public async Task Test_RunAsync_ShouldProduceDeterministicScore_WhenCalledTwiceWithSameNames()
    {
        // Arrange
        _context.Target.Returns("Alice,Bob");
        var scores = new List<int>();
        _context.When(c => c.ReplyRankAwareLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>()))
            .Do(call => scores.Add((int)call.ArgAt<object[]>(1)[2]));

        // Act
        await _command.RunAsync(_context);
        await _command.RunAsync(_context);

        // Assert
        Assert.That(scores, Has.Count.EqualTo(2));
        Assert.That(scores[0], Is.EqualTo(scores[1]));
    }

    [Test]
    public async Task Test_RunAsync_ShouldProduceSymmetricScore_WhenNamesAreSwapped()
    {
        // Arrange - character sorting normalises order so score(A,B) == score(B,A)
        var scores = new List<int>();
        _context.When(c => c.ReplyRankAwareLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>()))
            .Do(call => scores.Add((int)call.ArgAt<object[]>(1)[2]));

        // Act
        _context.Target.Returns("Alice,Bob");
        await _command.RunAsync(_context);

        _context.Target.Returns("Bob,Alice");
        await _command.RunAsync(_context);

        // Assert
        Assert.That(scores[0], Is.EqualTo(scores[1]));
    }

    // Emoji matches score - verified across multiple pairs to cover all branches

    [Test]
    [TestCase("Alice,Bob")]
    [TestCase("Elsa,Mina")]
    [TestCase("Pokemon,Showdown")]
    [TestCase("x,y")]
    [TestCase("aaaa,bbbb")]
    public async Task Test_RunAsync_ShouldReturnEmojiMatchingScore_ForGivenPair(string target)
    {
        // Arrange
        _context.Target.Returns(target);
        int capturedScore = -1;
        string capturedEmoji = null;
        _context.When(c => c.ReplyRankAwareLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>()))
            .Do(call =>
            {
                capturedScore = (int)call.ArgAt<object[]>(1)[2];
                capturedEmoji = (string)call.ArgAt<object[]>(1)[3];
            });

        // Act
        await _command.RunAsync(_context);

        // Assert
        var expectedEmoji = capturedScore switch
        {
            >= 90 => "💞",
            >= 70 => "❤️",
            >= 50 => "💛",
            >= 30 => "🤝",
            _ => "💔"
        };
        Assert.That(capturedEmoji, Is.EqualTo(expectedEmoji));
    }
}
