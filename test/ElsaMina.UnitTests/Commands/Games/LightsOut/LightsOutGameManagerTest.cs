using ElsaMina.Commands.Games.LightsOut;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.LightsOut;

[TestFixture]
public class LightsOutGameManagerTest
{
    private LightsOutGameManager _sut;

    [SetUp]
    public void SetUp()
    {
        _sut = new LightsOutGameManager();
    }

    [Test]
    public void Test_GetGame_ShouldReturnNull_WhenNoGameRegistered()
    {
        var result = _sut.GetGame("room1", "user1");

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Test_GetGame_ShouldReturnGame_WhenGameIsRegistered()
    {
        var game = Substitute.For<ILightsOutGame>();
        _sut.RegisterGame("room1", "user1", game);

        var result = _sut.GetGame("room1", "user1");

        Assert.That(result, Is.SameAs(game));
    }

    [Test]
    public void Test_GetGame_ShouldReturnNull_WhenDifferentUserOrRoom()
    {
        var game = Substitute.For<ILightsOutGame>();
        _sut.RegisterGame("room1", "user1", game);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_sut.GetGame("room1", "user2"), Is.Null);
            Assert.That(_sut.GetGame("room2", "user1"), Is.Null);
        }
    }

    [Test]
    public void Test_RegisterGame_ShouldOverwriteExistingGame_WhenSameKey()
    {
        var game1 = Substitute.For<ILightsOutGame>();
        var game2 = Substitute.For<ILightsOutGame>();
        _sut.RegisterGame("room1", "user1", game1);
        _sut.RegisterGame("room1", "user1", game2);

        var result = _sut.GetGame("room1", "user1");

        Assert.That(result, Is.SameAs(game2));
    }

    [Test]
    public void Test_RegisterGame_ShouldSupportMultipleUsers()
    {
        var game1 = Substitute.For<ILightsOutGame>();
        var game2 = Substitute.For<ILightsOutGame>();
        _sut.RegisterGame("room1", "user1", game1);
        _sut.RegisterGame("room1", "user2", game2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_sut.GetGame("room1", "user1"), Is.SameAs(game1));
            Assert.That(_sut.GetGame("room1", "user2"), Is.SameAs(game2));
        }
    }

    [Test]
    public void Test_RemoveGame_ShouldRemoveGame_WhenGameExists()
    {
        var game = Substitute.For<ILightsOutGame>();
        _sut.RegisterGame("room1", "user1", game);

        _sut.RemoveGame("room1", "user1");

        Assert.That(_sut.GetGame("room1", "user1"), Is.Null);
    }

    [Test]
    public void Test_RemoveGame_ShouldDoNothing_WhenGameDoesNotExist()
    {
        Assert.DoesNotThrow(() => _sut.RemoveGame("room1", "user1"));
    }

    [Test]
    public void Test_RegisterGame_ShouldAutoRemoveGame_WhenGameEnds()
    {
        var game = Substitute.For<ILightsOutGame>();
        _sut.RegisterGame("room1", "user1", game);

        game.GameEnded += Raise.Event<Action>();

        Assert.That(_sut.GetGame("room1", "user1"), Is.Null);
    }
}
