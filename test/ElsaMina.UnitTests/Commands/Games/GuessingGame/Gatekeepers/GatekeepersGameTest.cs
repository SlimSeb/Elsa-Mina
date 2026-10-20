using ElsaMina.Commands.Games.GuessingGame.Gatekeepers;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.GuessingGame.Gatekeepers;

public class GatekeepersGameTest
{
    private static readonly string[] SQUIRTLE_NAMES = ["Squirtle", "Carapuce"];

    private const string TEMPLATE_PATH = "Games/GuessingGame/Gatekeepers/GatekeepersGamePanel";

    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IDexManager _dexManager;
    private IRandomService _randomService;
    private IContext _context;
    private TestGatekeepersGame _game;
    private List<GatekeepersGamePanelViewModel> _renderedModels;
    private Pokemon[] _pokedex;

    [SetUp]
    public void SetUp()
    {
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _configuration.Name.Returns("ElsaBot");
        _configuration.Trigger.Returns("-");
        _dexManager = Substitute.For<IDexManager>();
        _randomService = Substitute.For<IRandomService>();
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("arcade");

        _renderedModels = [];
        _templatesManager.GetTemplateAsync(TEMPLATE_PATH, Arg.Any<object>()).Returns(callInfo =>
        {
            _renderedModels.Add(callInfo.ArgAt<GatekeepersGamePanelViewModel>(1));
            return Task.FromResult("<div>\npanel\n</div>");
        });

        _pokedex =
        [
            MakePokemon(1, "Bulbasaur", "Bulbizarre"),
            MakePokemon(4, "Charmander", "Salamèche"),
            MakePokemon(7, "Squirtle", "Carapuce"),
            MakePokemon(25, "Pikachu", "Pikachu"),
            MakePokemon(133, "Eevee", "Évoli")
        ];
        _dexManager.Pokedex.Returns(_pokedex);
        _randomService.RandomSample(Arg.Any<IEnumerable<Pokemon>>(), 4)
            .Returns(callInfo => callInfo.Arg<IEnumerable<Pokemon>>().Take(4));
        _randomService.RandomElement(Arg.Any<IEnumerable<Pokemon>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Pokemon>>().ElementAt(2));

        _game = new TestGatekeepersGame(_templatesManager, _configuration, _dexManager, _randomService,
            Substitute.For<IClockService>())
        {
            Context = _context
        };
    }

    private static Pokemon MakePokemon(int pokedexId, string english, string french) => new()
    {
        PokedexId = pokedexId,
        Name = new Name { English = english, French = french }
    };

    [Test]
    public void Test_Identifier_ShouldReturnGatekeepersGame()
    {
        Assert.That(_game.Identifier, Is.EqualTo("GatekeepersGame"));
    }

    [Test]
    public async Task Test_OnTurnStart_ShouldPickFourOptionsAndOneAnswer()
    {
        // Act
        await _game.CallOnTurnStart();

        // Assert
        var model = _renderedModels.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_game.ValidAnswers, Is.EqualTo(SQUIRTLE_NAMES));
            Assert.That(model.CurrentOptions, Has.Count.EqualTo(4));
            Assert.That(model.CurrentValidAnswer, Is.SameAs(_pokedex[2]));
            Assert.That(model.FootprintSprite, Does.EndWith("/Gen5Footprints/7.png"));
            Assert.That(model.ShowPortraits, Is.False);
            Assert.That(model.ShowSilhouettes, Is.False);
            Assert.That(model.ShouldShowCorrectAnswer, Is.False);
            Assert.That(model.BotName, Is.EqualTo("ElsaBot"));
            Assert.That(model.RoomId, Is.EqualTo("arcade"));
        }
        _context.Received(1).SendUpdatableHtml(Arg.Is<string>(id => id.StartsWith("gatekeepers-")),
            "<div>panel</div>", false);
    }

    [Test]
    public async Task Test_OnTimerCountdown_ShouldRevealPortraits_WhenTenSecondsRemain()
    {
        // Arrange
        await _game.CallOnTurnStart();

        // Act
        _game.CallOnTimerCountdown(TimeSpan.FromSeconds(10));

        // Assert
        var model = _renderedModels[^1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.ShowPortraits, Is.True);
            Assert.That(model.ShowSilhouettes, Is.False);
            Assert.That(model.RemainingTime, Is.EqualTo(TimeSpan.FromSeconds(10)));
        }
        _context.Received(1).SendUpdatableHtml(Arg.Any<string>(), Arg.Any<string>(), true);
    }

    [Test]
    public async Task Test_OnTimerCountdown_ShouldRevealSilhouettes_WhenFiveSecondsRemain()
    {
        // Arrange
        await _game.CallOnTurnStart();

        // Act
        _game.CallOnTimerCountdown(TimeSpan.FromSeconds(5));

        // Assert
        var model = _renderedModels[^1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.ShowPortraits, Is.True);
            Assert.That(model.ShowSilhouettes, Is.True);
        }
        _context.Received(1).ReplyLocalizedMessage("countries_game_turn_ending_soon", 5);
    }

    [Test]
    public async Task Test_OnTimerCountdown_ShouldNotRedraw_OnOtherTicks()
    {
        // Arrange
        await _game.CallOnTurnStart();

        // Act
        _game.CallOnTimerCountdown(TimeSpan.FromSeconds(12));

        // Assert
        Assert.That(_renderedModels, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Test_OnTimerCountdown_ShouldNotRedraw_WhenRoundIsWon()
    {
        // Arrange
        await _game.CallOnTurnStart();
        _game.OnAnswer("Player", "Carapuce");
        var renderCount = _renderedModels.Count;

        // Act
        _game.CallOnTimerCountdown(TimeSpan.FromSeconds(10));

        // Assert
        Assert.That(_renderedModels, Has.Count.EqualTo(renderCount));
    }

    [Test]
    public async Task Test_OnAnswer_ShouldRevealCorrectAnswer_WhenAnswerIsRight()
    {
        // Arrange
        await _game.CallOnTurnStart();

        // Act
        _game.OnAnswer("Player", "squirtle");

        // Assert
        var model = _renderedModels[^1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(model.ShouldShowCorrectAnswer, Is.True);
            Assert.That(model.ShowPortraits, Is.True);
            Assert.That(model.RemainingTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(model.Scores.Values.Single(), Is.EqualTo(1));
        }
        _context.Received(1).ReplyLocalizedMessage("guessing_game_round_won", "Player", 1, string.Empty);
    }

    private class TestGatekeepersGame : GatekeepersGame
    {
        public TestGatekeepersGame(ITemplatesManager templatesManager, IConfiguration configuration,
            IDexManager dexManager, IRandomService randomService, IClockService clockService)
            : base(templatesManager, configuration, dexManager, randomService, clockService)
        {
        }

        public IEnumerable<string> ValidAnswers => CurrentValidAnswers;

        public Task CallOnTurnStart() => OnTurnStart();

        public void CallOnTimerCountdown(TimeSpan remainingTime) => OnTimerCountdown(remainingTime);
    }
}
