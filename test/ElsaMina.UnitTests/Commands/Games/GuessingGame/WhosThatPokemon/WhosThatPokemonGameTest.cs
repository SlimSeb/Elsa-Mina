using ElsaMina.Commands.Games.GuessingGame.WhosThatPokemon;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.GuessingGame.WhosThatPokemon;

public class WhosThatPokemonGameTest
{
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IClockService _clockService;
    private IDexManager _dexManager;
    private IRandomService _randomService;
    private IContext _context;
    private TestWhosThatPokemonGame _game;

    [SetUp]
    public void SetUp()
    {
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _clockService = Substitute.For<IClockService>();
        _dexManager = Substitute.For<IDexManager>();
        _randomService = Substitute.For<IRandomService>();
        _context = Substitute.For<IContext>();

        _configuration.Name.Returns("ElsaBot");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<template/>");

        _game = new TestWhosThatPokemonGame(_templatesManager, _configuration, _randomService, _dexManager,
            _clockService)
        {
            Context = _context
        };
    }

    private static Pokemon MakePokemon(int pokedexId, string english, string french, string japanese) => new()
    {
        PokedexId = pokedexId,
        Name = new Name { English = english, French = french, Japanese = japanese },
        Sprites = new Sprite { Regular = $"https://example.com/{pokedexId}.png" }
    };

    [Test]
    public async Task Test_OnTurnStart_ShouldSetValidAnswersToPokemonNames_WhenTurnStarts()
    {
        // Arrange
        var missingNo = MakePokemon(0, "MissingNo.", "MissingNo.", "-");
        var pikachu = MakePokemon(25, "Pikachu", "Pikachu", "ピカチュウ");
        _dexManager.Pokedex.Returns(new[] { missingNo, pikachu });
        _randomService.NextInt(Arg.Any<int>(), Arg.Any<int>()).Returns(1);

        // Act
        await _game.CallOnTurnStart();

        // Assert
        Assert.That(_game.ValidAnswers, Does.Contain("Pikachu"));
        Assert.That(_game.ValidAnswers, Does.Contain("ピカチュウ"));
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldSendSilhouettePanel_WhenTurnStarts()
    {
        // Arrange
        var missingNo = MakePokemon(0, "MissingNo.", "MissingNo.", "-");
        var bulbasaur = MakePokemon(1, "Bulbasaur", "Bulbizarre", "フシギダネ");
        _dexManager.Pokedex.Returns(new[] { missingNo, bulbasaur });
        _randomService.NextInt(Arg.Any<int>(), Arg.Any<int>()).Returns(1);

        // Act
        await _game.CallOnTurnStart();

        // Assert
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), false);
    }

    [Test]
    public async Task Test_OnCorrectAnswer_ShouldSendRevealedPanel_WhenAnswerIsCorrect()
    {
        // Arrange
        var missingNo = MakePokemon(0, "MissingNo.", "MissingNo.", "-");
        var bulbasaur = MakePokemon(1, "Bulbasaur", "Bulbizarre", "フシギダネ");
        _dexManager.Pokedex.Returns(new[] { missingNo, bulbasaur });
        _randomService.NextInt(Arg.Any<int>(), Arg.Any<int>()).Returns(1);
        await _game.CallOnTurnStart();
        _context.ClearReceivedCalls();

        // Act
        _game.CallOnCorrectAnswer();

        // Assert
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), true);
    }

    private class TestWhosThatPokemonGame : WhosThatPokemonGame
    {
        public TestWhosThatPokemonGame(ITemplatesManager templatesManager, IConfiguration configuration,
            IRandomService randomService, IDexManager dexManager, IClockService clockService)
            : base(templatesManager, configuration, randomService, dexManager, clockService)
        {
        }

        public IEnumerable<string> ValidAnswers => CurrentValidAnswers;

        public Task CallOnTurnStart() => OnTurnStart();

        public void CallOnCorrectAnswer() => OnCorrectAnswer();
    }
}
