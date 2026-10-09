using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.GuessingGame.Gatekeepers;

public class GatekeepersGame : GuessingGame
{
    private const int ANSWERS_COUNT = 4;
    private const string TEMPLATE_PATH = "Games/GuessingGame/Gatekeepers/GatekeepersGamePanel";

    private const string FOOTPRINT_SPRITE_URL = // C'est très con oui
        "https://raw.githubusercontent.com/SlimSeb/SpriteCollection/refs/heads/main/Gen5Footprints/{0}.png";

    private static readonly TimeSpan SHOW_PORTRAITS_AT = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SHOW_SILHOUETTES_AT = TimeSpan.FromSeconds(5);
    private const int MAX_SPECIES_ID = 649; // Gen 1 à 5

    private static int NextGameId { get; set; } = 1;

    private readonly IDexManager _dexManager;
    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly int _gameId;
    private readonly List<Pokemon> _currentOptions = [];
    private Pokemon _currentValidAnswer;

    public GatekeepersGame(ITemplatesManager templatesManager, IConfiguration configuration,
        IDexManager dexManager, IRandomService randomService, IClockService clockService)
        : base(templatesManager, configuration, clockService)
    {
        _templatesManager = templatesManager;
        _configuration = configuration;
        _dexManager = dexManager;
        _randomService = randomService;

        _gameId = NextGameId++;
    }

    public override string Identifier => nameof(GatekeepersGame);

    protected override bool HasCooldown => true;

    private string HtmlId => $"gatekeepers-{_gameId}-t{CurrentTurn}";

    protected override void OnGameStart()
    {
        // Do nothing
    }

    protected override async Task OnTurnStart()
    {
        var slice = _dexManager.Pokedex.Take(MAX_SPECIES_ID);
        var sample = _randomService.RandomSample(slice, ANSWERS_COUNT).ToList();

        _currentOptions.Clear();
        _currentOptions.AddRange(sample);
        _currentValidAnswer = _randomService.RandomElement(_currentOptions);
        CurrentValidAnswers = [_currentValidAnswer.Name.English, _currentValidAnswer.Name.French];

        var template = await _templatesManager.GetTemplateAsync(TEMPLATE_PATH,
            BuildViewModel(DEFAULT_TURN_COOLDOWN, showPortraits: false, showSilhouettes: false,
                showCorrectAnswer: false));
        Context.SendUpdatableHtml(HtmlId, template.RemoveNewlines(), isChanging: false);
    }

    protected override void OnTimerCountdown(TimeSpan remainingTime)
    {
        base.OnTimerCountdown(remainingTime);

        if (HasRoundBeenWon)
        {
            return;
        }

        if (remainingTime == SHOW_PORTRAITS_AT)
        {
            var template = _templatesManager.GetTemplateAsync(TEMPLATE_PATH,
                    BuildViewModel(remainingTime, showPortraits: true, showSilhouettes: false,
                        showCorrectAnswer: false))
                .Result;
            Context.SendUpdatableHtml(HtmlId, template.RemoveNewlines(), isChanging: true);
        }
        else if (remainingTime == SHOW_SILHOUETTES_AT)
        {
            var template = _templatesManager.GetTemplateAsync(TEMPLATE_PATH,
                    BuildViewModel(remainingTime, showPortraits: true, showSilhouettes: true, showCorrectAnswer: false))
                .Result;
            Context.SendUpdatableHtml(HtmlId, template.RemoveNewlines(), isChanging: true);
        }
    }

    protected override void OnCorrectAnswer()
    {
        base.OnCorrectAnswer();
        var template = _templatesManager.GetTemplateAsync(TEMPLATE_PATH,
            BuildViewModel(TimeSpan.Zero, showPortraits: true, showSilhouettes: true, showCorrectAnswer: true)).Result;
        Context.SendUpdatableHtml(HtmlId, template.RemoveNewlines(), isChanging: true);
    }

    private GatekeepersGamePanelViewModel BuildViewModel(TimeSpan remainingTime, bool showPortraits,
        bool showSilhouettes, bool showCorrectAnswer) =>
        new()
        {
            Culture = Context.Culture,
            FootprintSprite = string.Format(FOOTPRINT_SPRITE_URL, _currentValidAnswer.PokedexId),
            CurrentOptions = _currentOptions.AsReadOnly(),
            ShowPortraits = showPortraits,
            ShowSilhouettes = showSilhouettes,
            CurrentValidAnswer = _currentValidAnswer,
            ShouldShowCorrectAnswer = showCorrectAnswer,
            Scores = Scores,
            CurrentTurn = CurrentTurn,
            TurnsCount = TurnsCount,
            RemainingTime = remainingTime,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = Context.RoomId
        };
}