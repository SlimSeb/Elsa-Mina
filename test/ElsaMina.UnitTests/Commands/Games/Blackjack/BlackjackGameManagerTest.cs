using ElsaMina.Commands.Games.Blackjack;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Blackjack;

public class BlackjackGameManagerTest
{
    private BlackjackGameManager _manager;

    [SetUp]
    public void SetUp()
    {
        _manager = new BlackjackGameManager();
    }

    [Test]
    public void Test_GetGame_ShouldReturnNull_WhenNoGameRegistered()
    {
        Assert.That(_manager.GetGame("room", "user"), Is.Null);
    }

    [Test]
    public void Test_RegisterGame_ShouldMakeGameRetrievableForRoomAndUser()
    {
        // Arrange
        var game = Substitute.For<IBlackjackGame>();

        // Act
        _manager.RegisterGame("room", "user", game);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_manager.GetGame("room", "user"), Is.SameAs(game));
            Assert.That(_manager.GetGame("room", "other"), Is.Null);
            Assert.That(_manager.GetGame("other", "user"), Is.Null);
        }
    }

    [Test]
    public void Test_RegisterGame_ShouldRemoveGame_WhenGameEnds()
    {
        // Arrange
        var game = Substitute.For<IBlackjackGame>();
        _manager.RegisterGame("room", "user", game);

        // Act
        game.GameEnded += Raise.Event<Action>();

        // Assert
        Assert.That(_manager.GetGame("room", "user"), Is.Null);
    }

    [Test]
    public void Test_RemoveGame_ShouldRemoveRegisteredGame()
    {
        // Arrange
        _manager.RegisterGame("room", "user", Substitute.For<IBlackjackGame>());

        // Act
        _manager.RemoveGame("room", "user");

        // Assert
        Assert.That(_manager.GetGame("room", "user"), Is.Null);
    }
}
