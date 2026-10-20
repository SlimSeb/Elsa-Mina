using ElsaMina.Commands.Games.GuessingGame;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.GuessingGame;

public class GuessingGameFactoryTest
{
    private Func<ElsaMina.Commands.Games.GuessingGame.GuessingGame> _countriesGameFactory;
    private Func<ElsaMina.Commands.Games.GuessingGame.GuessingGame> _triviaGameFactory;
    private GuessingGameFactory _factory;

    [SetUp]
    public void SetUp()
    {
        _countriesGameFactory = Substitute.For<Func<ElsaMina.Commands.Games.GuessingGame.GuessingGame>>();
        _triviaGameFactory = Substitute.For<Func<ElsaMina.Commands.Games.GuessingGame.GuessingGame>>();
        _factory = new GuessingGameFactory([
            new GuessingGameRegistration("countriesgame", _countriesGameFactory),
            new GuessingGameRegistration("trivia", _triviaGameFactory)
        ]);
    }

    [Test]
    public void Test_Create_ShouldUseMatchingFactory_WhenCommandIsKnown()
    {
        // Act
        _factory.Create("countriesgame");

        // Assert
        _countriesGameFactory.Received(1)();
        _triviaGameFactory.DidNotReceive()();
    }

    [Test]
    public void Test_Create_ShouldReturnNull_WhenCommandIsUnknown()
    {
        // Act
        var game = _factory.Create("unknown");

        // Assert
        Assert.That(game, Is.Null);
        _countriesGameFactory.DidNotReceive()();
        _triviaGameFactory.DidNotReceive()();
    }

    [Test]
    public void Test_Create_ShouldReturnNull_WhenCommandIsNull()
    {
        // Act
        var game = _factory.Create(null);

        // Assert
        Assert.That(game, Is.Null);
    }
}
