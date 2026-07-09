using ElsaMina.Commands.Games.Scattergories;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Scattergories;

public class ScattergoriesGameTest
{
    private IDexManager _dexManager;
    private IRandomService _randomService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private ISystemService _systemService;
    private IContext _context;
    private ScattergoriesGame _game;

    [SetUp]
    public void SetUp()
    {
        _dexManager = Substitute.For<IDexManager>();
        _randomService = Substitute.For<IRandomService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _systemService = Substitute.For<ISystemService>();
        _context = Substitute.For<IContext>();

        _configuration.Name.Returns("ElsaBot");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult("<html></html>"));
        _systemService.SleepAsync(Arg.Any<TimeSpan>()).Returns(Task.CompletedTask);

        // A tiny Pokédex: index 0 is the usual placeholder. No letter reaches the
        // MIN_POKEMON_PER_LETTER threshold, so PickLetter() deterministically falls back to 'p'.
        _dexManager.Pokedex.Returns(new[]
        {
            null,
            MakePokemon(25, "Pikachu", "Pikachu"),
            MakePokemon(172, "Pichu", "Pichu"),
            MakePokemon(1, "Bulbasaur", "Bulbizarre")
        });

        _game = new ScattergoriesGame(_dexManager, _randomService, _templatesManager, _configuration,
            _systemService);
        _game.Context = _context;
    }

    [TearDown]
    public void TearDown()
    {
        _game.Cancel();
    }

    [Test]
    public async Task Test_OnAnswer_ShouldScore_WhenPokemonStartsWithLetter()
    {
        await _game.StartAsync();

        _game.OnAnswer("Player1", "pikachu");

        _context.Received(1).ReplyLocalizedMessage("scattergories_answer_scored", "Player1", "Pikachu", 1);
    }

    [Test]
    public async Task Test_OnAnswer_ShouldIgnore_WhenPokemonDoesNotStartWithLetter()
    {
        await _game.StartAsync();

        _game.OnAnswer("Player1", "bulbasaur");

        _context.DidNotReceive().ReplyLocalizedMessage("scattergories_answer_scored", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_OnAnswer_ShouldIgnore_WhenSenderIsBot()
    {
        await _game.StartAsync();

        _game.OnAnswer("ElsaBot", "pikachu");

        _context.DidNotReceive().ReplyLocalizedMessage("scattergories_answer_scored", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_OnAnswer_ShouldNotScoreSamePokemonTwice()
    {
        await _game.StartAsync();

        _game.OnAnswer("Player1", "pikachu");
        _game.OnAnswer("Player2", "pikachu");

        _context.Received(1).ReplyLocalizedMessage("scattergories_answer_scored", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_OnAnswer_ShouldScoreDistinctPokemon_ForDifferentPlayers()
    {
        await _game.StartAsync();

        _game.OnAnswer("Player1", "pikachu");
        _game.OnAnswer("Player2", "pichu");

        _context.Received(1).ReplyLocalizedMessage("scattergories_answer_scored", "Player1", "Pikachu", 1);
        _context.Received(1).ReplyLocalizedMessage("scattergories_answer_scored", "Player2", "Pichu", 1);
    }

    [Test]
    public async Task Test_OnAnswer_ShouldIgnore_WhenGameHasEnded()
    {
        await _game.StartAsync();
        _game.Cancel();

        _game.OnAnswer("Player1", "pikachu");

        _context.DidNotReceive().ReplyLocalizedMessage("scattergories_answer_scored", Arg.Any<object[]>());
    }

    private static Pokemon MakePokemon(int id, string english, string french) => new()
    {
        PokedexId = id,
        Name = new Name { English = english, French = french },
        Sprites = new Sprite { Regular = $"https://sprites/{id}.png" }
    };
}
