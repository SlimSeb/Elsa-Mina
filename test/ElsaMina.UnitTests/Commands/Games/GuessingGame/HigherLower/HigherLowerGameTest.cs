using ElsaMina.Commands.Games.GuessingGame.HigherLower;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.GuessingGame.HigherLower;

public class HigherLowerGameTest
{
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IClockService _clockService;
    private IDexManager _dexManager;
    private IRandomService _randomService;
    private IContext _context;
    private TestHigherLowerGame _game;

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

        _game = new TestHigherLowerGame(_templatesManager, _configuration, _dexManager, _randomService, _clockService)
        {
            Context = _context
        };
    }

    private static Pokemon MakePokemon(int pokedexId, int baseStat, string weight = "10 kg") => new()
    {
        PokedexId = pokedexId,
        Weight = weight,
        Height = "1 m",
        Stats = new Stats
        {
            HP = baseStat,
            Attack = baseStat,
            Defense = baseStat,
            SpecialAttack = baseStat,
            SpecialDefense = baseStat,
            Speed = baseStat
        }
    };

    private void SetupCategory(string labelKey) =>
        _randomService.RandomElement(Arg.Any<IEnumerable<HigherLowerCategory>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<HigherLowerCategory>>().First(c => c.LabelKey == labelKey));

    private void SetupPair(Pokemon first, Pokemon second)
    {
        _dexManager.Pokedex.Returns(new[] { first, second });
        _randomService.RandomElement(Arg.Any<IEnumerable<Pokemon>>()).Returns(first, second);
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldExpectHigherAnswers_WhenSecondBaseStatTotalIsGreater()
    {
        SetupCategory("higherlower_category_bst");
        SetupPair(MakePokemon(1, baseStat: 50), MakePokemon(2, baseStat: 100));

        await _game.CallOnTurnStart();

        Assert.That(_game.ValidAnswers, Does.Contain("higher"));
        Assert.That(_game.ValidAnswers, Does.Not.Contain("lower"));
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldExpectLowerAnswers_WhenSecondDexNumberIsSmaller()
    {
        SetupCategory("higherlower_category_dex");
        SetupPair(MakePokemon(150, baseStat: 60), MakePokemon(10, baseStat: 60));

        await _game.CallOnTurnStart();

        Assert.That(_game.ValidAnswers, Does.Contain("lower"));
        Assert.That(_game.ValidAnswers, Does.Not.Contain("higher"));
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldParseCommaSeparatedWeights_WhenCategoryIsWeight()
    {
        SetupCategory("higherlower_category_weight");
        SetupPair(MakePokemon(1, baseStat: 60, weight: "6,9 kg"), MakePokemon(2, baseStat: 60, weight: "90,5 kg"));

        await _game.CallOnTurnStart();

        Assert.That(_game.ValidAnswers, Does.Contain("higher"));
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldSendPanel()
    {
        SetupCategory("higherlower_category_bst");
        SetupPair(MakePokemon(1, baseStat: 50), MakePokemon(2, baseStat: 100));

        await _game.CallOnTurnStart();

        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    private class TestHigherLowerGame : HigherLowerGame
    {
        public TestHigherLowerGame(ITemplatesManager templatesManager, IConfiguration configuration,
            IDexManager dexManager, IRandomService randomService, IClockService clockService)
            : base(templatesManager, configuration, dexManager, randomService, clockService)
        {
        }

        public IEnumerable<string> ValidAnswers => CurrentValidAnswers;

        public Task CallOnTurnStart() => OnTurnStart();
    }
}
