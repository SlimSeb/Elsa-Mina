using System.Globalization;
using ElsaMina.Commands.Games.Semantix;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Semantix;

public class GuessSemantixCommandTest
{
    private IRoomsManager _roomsManager;
    private ISemantixGameManager _gameManager;
    private GuessSemantixCommand _command;
    private IContext _context;
    private IUser _sender;
    private ISemantixGame _game;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<ISemantixGameManager>();
        _command = new GuessSemantixCommand(_roomsManager, _gameManager);

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("arcade");
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("player");
        _context.Sender.Returns(_sender);
        _game = Substitute.For<ISemantixGame>();
    }

    [Test]
    public void Test_Properties_ShouldBeConfiguredForRegularUsersInPrivateMessages()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
            Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
            Assert.That(_command.HelpMessageKey, Is.EqualTo("sx_guess_help"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFormat_WhenPrivateMessageHasNoWord()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("arcade");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("sx_guess_pm_format");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoGame_WhenNoGameIsRunning()
    {
        // Arrange
        _context.Target.Returns("pomme");
        _gameManager.GetGame("arcade", "player").ReturnsNull();

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("sx_game_no_game");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSubmitGuess_WhenGuessingInRoom()
    {
        // Arrange
        _context.Target.Returns(" pomme ");
        _gameManager.GetGame("arcade", "player").Returns(_game);
        _game.SubmitGuess(_sender, "pomme").Returns(SemantixGuessOutcome.Accepted);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.Received(1).SubmitGuess(_sender, "pomme");
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSubmitGuessToPrivateGame_WhenGuessingInPrivateMessage()
    {
        // Arrange
        var culture = new CultureInfo("fr-FR");
        var room = Substitute.For<IRoom>();
        room.Culture.Returns(culture);
        _roomsManager.GetRoom("casino").Returns(room);
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("casino, pomme");
        _game.IsPrivateMode.Returns(true);
        _gameManager.GetGame("casino", "player").Returns(_game);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _game.Received(1).SubmitGuess(_sender, "pomme");
        _context.Received().Culture = culture;
        Assert.That(_game.Context, Is.SameAs(_context));
    }

    [TestCase(SemantixGuessOutcome.RoundNotActive, "sx_guess_round_not_active")]
    [TestCase(SemantixGuessOutcome.NotOwner, "sx_guess_not_owner")]
    [TestCase(SemantixGuessOutcome.EmptyWord, "sx_guess_empty")]
    [TestCase(SemantixGuessOutcome.NotInWordList, "sx_guess_not_in_list")]
    [TestCase(SemantixGuessOutcome.AlreadyGuessed, "sx_guess_already_guessed")]
    [TestCase(SemantixGuessOutcome.EmbeddingUnavailable, "sx_game_api_unavailable")]
    public async Task Test_RunAsync_ShouldReplyOutcomeMessage_WhenGuessIsRejected(SemantixGuessOutcome outcome,
        string expectedKey)
    {
        // Arrange
        _context.Target.Returns("pomme");
        _gameManager.GetGame("arcade", "player").Returns(_game);
        _game.SubmitGuess(_sender, "pomme").Returns(outcome);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage(expectedKey);
    }
}
