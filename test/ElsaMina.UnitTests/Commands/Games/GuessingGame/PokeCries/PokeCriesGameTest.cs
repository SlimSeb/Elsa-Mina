using ElsaMina.Commands.Games.GuessingGame.PokeCries;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.GuessingGame.PokeCries;

public class PokeCriesGameTest
{
    private IDexManager _dexManager;
    private IRandomService _randomService;
    private IContext _context;
    private TestPokeCriesGame _game;

    [SetUp]
    public void SetUp()
    {
        _dexManager = Substitute.For<IDexManager>();
        _randomService = Substitute.For<IRandomService>();
        _context = Substitute.For<IContext>();
        _dexManager.Pokedex.Returns(
        [
            MakePokemon(0, "MissingNo.", "MissingNo.", "-"),
            MakePokemon(1, "Bulbasaur", "Bulbizarre", "Fushigidane"),
            MakePokemon(2, "Ivysaur", "Herbizarre", "Fushigisou")
        ]);
        _randomService.NextDouble().Returns(0.5);

        _game = new TestPokeCriesGame(Substitute.For<ITemplatesManager>(), Substitute.For<IConfiguration>(),
            _randomService, _dexManager, Substitute.For<IClockService>())
        {
            Context = _context
        };
    }

    private static Pokemon MakePokemon(int pokedexId, string english, string french, string japanese) => new()
    {
        PokedexId = pokedexId,
        Name = new Name { English = english, French = french, Japanese = japanese }
    };

    [Test]
    public void Test_Identifier_ShouldReturnPokeCriesGame()
    {
        Assert.That(_game.Identifier, Is.EqualTo("PokeCriesGame"));
    }

    [Test]
    public void Test_OnGameStart_ShouldAnnounceGame()
    {
        // Act
        _game.CallOnGameStart();

        // Assert
        _context.Received(1).ReplyLocalizedMessage("pokecries_started");
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldPlayPokemonCry_AndAcceptItsNames()
    {
        // Arrange
        _randomService.NextInt(1, 808).Returns(2);

        // Act
        await _game.CallOnTurnStart();

        // Assert
        Assert.That(_game.ValidAnswers, Is.EqualTo(new[] { "Herbizarre", "Ivysaur", "Fushigisou" }));
        _context.Received(1).ReplyHtml(
            "<audio controls src=\"https://media.pokemoncentral.it/wiki/versi/002.mp3\"></audio>");
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldDoNothing_WhenPokemonIsOutsideThePokedex()
    {
        // Arrange
        _randomService.NextInt(1, 808).Returns(500);

        // Act
        await _game.CallOnTurnStart();

        // Assert
        Assert.That(_game.ValidAnswers, Is.Empty);
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldPlayEasterEgg_WhenRollIsLucky()
    {
        // Arrange
        _randomService.NextDouble().Returns(0.01);
        _randomService.RandomElement(Arg.Any<IEnumerable<EasterEgg>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<EasterEgg>>().Last());

        // Act
        await _game.CallOnTurnStart();

        // Assert
        Assert.That(_game.ValidAnswers, Is.EqualTo(new[] { "Sardoche" }));
        _context.Received(1).ReplyHtml(Arg.Is<string>(html => html.Contains("sardoche")), Arg.Any<string>(),
            Arg.Any<bool>());
        _randomService.DidNotReceive().NextInt(Arg.Any<int>(), Arg.Any<int>());
    }

    private class TestPokeCriesGame : PokeCriesGame
    {
        public TestPokeCriesGame(ITemplatesManager templatesManager, IConfiguration configuration,
            IRandomService randomService, IDexManager dexManager, IClockService clockService)
            : base(templatesManager, configuration, randomService, dexManager, clockService)
        {
        }

        public IEnumerable<string> ValidAnswers => CurrentValidAnswers;

        public Task CallOnTurnStart() => OnTurnStart();

        public void CallOnGameStart() => OnGameStart();
    }
}
