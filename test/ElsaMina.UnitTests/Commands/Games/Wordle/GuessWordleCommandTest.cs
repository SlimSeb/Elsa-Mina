using ElsaMina.Commands.Games.Wordle;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Wordle;

[TestFixture]
public class GuessWordleCommandTest
{
    private IRoomsManager _roomsManager;
    private IWordleGameManager _gameManager;
    private IContext _context;
    private IUser _sender;
    private IWordleGame _game;
    private GuessWordleCommand _sut;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<IWordleGameManager>();
        _context = Substitute.For<IContext>();
        _game = Substitute.For<IWordleGame>();

        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("player");
        _context.Sender.Returns(_sender);
        _context.IsPrivateMessage.Returns(true);

        _gameManager.GetGame(Arg.Any<string>(), Arg.Any<string>()).ReturnsNull();

        _sut = new GuessWordleCommand(_roomsManager, _gameManager);
    }

    [Test]
    public void Test_Properties_ShouldOnlyAllowPrivateMessages()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_sut.Name, Is.EqualTo("wordleguess"));
            Assert.That(_sut.Aliases, Does.Contain("wlg"));
            // A guess typed in a room would spoil the daily word for everyone watching.
            Assert.That(_sut.IsPrivateMessageOnly, Is.True);
            Assert.That(_sut.IsAllowedInPrivateMessage, Is.True);
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldSubmitWord_WhenTargetIsValid()
    {
        // Arrange
        _context.Target.Returns("myroom, crane");
        _gameManager.GetGame("myroom", "player").Returns(_game);
        _game.SubmitWord(_sender, "crane").Returns(WordleGuessOutcome.Accepted);

        // Act
        await _sut.RunAsync(_context);

        // Assert
        await _game.Received().SubmitWord(_sender, "crane");
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFormat_WhenRoomIsMissing()
    {
        // Arrange
        _context.Target.Returns("crane");

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("wordle_guess_pm_format");
        await _game.DidNotReceive().SubmitWord(Arg.Any<IUser>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoGame_WhenSenderHasNoGameInThatRoom()
    {
        // Arrange
        _context.Target.Returns("myroom, crane");

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("wordle_game_no_game");
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotHijackContext_WhenGameIsNotInPrivateMode()
    {
        // Arrange
        _context.Target.Returns("myroom, crane");
        _game.IsPrivateMode.Returns(false);
        _gameManager.GetGame("myroom", "player").Returns(_game);

        // Act
        await _sut.RunAsync(_context);

        // Assert: the room game keeps rendering its panels through its own room context
        _game.DidNotReceiveWithAnyArgs().Context = Arg.Any<IContext>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoundNotActive_WhenRoundIsOver()
    {
        // Arrange
        _context.Target.Returns("myroom, crane");
        _gameManager.GetGame("myroom", "player").Returns(_game);
        _game.SubmitWord(_sender, "crane").Returns(WordleGuessOutcome.RoundNotActive);

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("wordle_guess_round_not_active");
    }
}
