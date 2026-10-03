using ElsaMina.Commands.Games.GuessingGame;
using ElsaMina.Commands.Games.GuessingGame.Capitals;
using ElsaMina.Commands.Games.GuessingGame.Countries;
using ElsaMina.Commands.Games.GuessingGame.Gatekeepers;
using ElsaMina.Commands.Games.GuessingGame.HigherLower;
using ElsaMina.Commands.Games.GuessingGame.PokeCries;
using ElsaMina.Commands.Games.GuessingGame.PokeDesc;
using ElsaMina.Commands.Games.GuessingGame.Trivia;
using ElsaMina.Commands.Games.GuessingGame.WhosThatPokemon;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.GuessingGame;

public class GuessingGameFactoryTest
{
    private Func<CountriesGame> _countriesGameFactory;
    private Func<TriviaGame> _triviaGameFactory;
    private GuessingGameFactory _factory;

    [SetUp]
    public void SetUp()
    {
        _countriesGameFactory = Substitute.For<Func<CountriesGame>>();
        _triviaGameFactory = Substitute.For<Func<TriviaGame>>();
        _factory = new GuessingGameFactory(
            _countriesGameFactory,
            Substitute.For<Func<PokeDescGame>>(),
            Substitute.For<Func<PokeCriesGame>>(),
            Substitute.For<Func<GatekeepersGame>>(),
            Substitute.For<Func<CapitalCitiesGame>>(),
            Substitute.For<Func<HigherLowerGame>>(),
            Substitute.For<Func<WhosThatPokemonGame>>(),
            _triviaGameFactory);
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
