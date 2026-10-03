using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.GuessingGame.WhosThatPokemon;

public class WhosThatPokemonGame : GuessingGame
{
    private const int MAX_SPECIES_ID = 1025;
    private const string TEMPLATE_PATH = "Games/GuessingGame/WhosThatPokemon/WhosThatPokemonPanel";

    private static int NextGameId { get; set; } = 1;

    private readonly ITemplatesManager _templatesManager;
    private readonly IRandomService _randomService;
    private readonly IDexManager _dexManager;
    private readonly int _gameId;

    private Pokemon _currentPokemon;

    public WhosThatPokemonGame(ITemplatesManager templatesManager,
        IConfiguration configuration,
        IRandomService randomService,
        IDexManager dexManager,
        IClockService clockService) : base(templatesManager, configuration, clockService)
    {
        _templatesManager = templatesManager;
        _randomService = randomService;
        _dexManager = dexManager;
        _gameId = NextGameId++;
    }

    public override string Identifier => nameof(WhosThatPokemonGame);

    protected override bool HasCooldown => true;

    private string HtmlId => $"whosthatpokemon-{_gameId}-t{CurrentTurn}";

    protected override void OnGameStart()
    {
        Context.ReplyLocalizedMessage("whosthatpokemon_start");
    }

    protected override async Task OnTurnStart()
    {
        _currentPokemon = PickRandomPokemon();
        CurrentValidAnswers =
        [
            _currentPokemon.Name.English,
            _currentPokemon.Name.French,
            _currentPokemon.Name.Japanese
        ];

        var template = await _templatesManager.GetTemplateAsync(TEMPLATE_PATH,
            BuildViewModel(DEFAULT_TURN_COOLDOWN, isRevealed: false));
        Context.SendUpdatableHtml(HtmlId, template.RemoveNewlines(), isChanging: false);
    }

    protected override void OnTimerCountdown(TimeSpan remainingTime)
    {
        base.OnTimerCountdown(remainingTime);

        // When the turn runs out without a winner, unveil the silhouette so
        // players can see which Pokémon it was.
        if (remainingTime == TimeSpan.Zero && !HasRoundBeenWon)
        {
            Reveal();
        }
    }

    protected override void OnCorrectAnswer()
    {
        base.OnCorrectAnswer();
        Reveal();
    }

    private void Reveal()
    {
        var template = _templatesManager
            .GetTemplateAsync(TEMPLATE_PATH, BuildViewModel(TimeSpan.Zero, isRevealed: true))
            .Result;
        Context.SendUpdatableHtml(HtmlId, template.RemoveNewlines(), isChanging: true);
    }

    private Pokemon PickRandomPokemon()
    {
        var speciesId = _randomService.NextInt(1, MAX_SPECIES_ID + 1);
        if (speciesId >= _dexManager.Pokedex.Length)
        {
            speciesId = _dexManager.Pokedex.Length - 1;
        }

        return _dexManager.Pokedex[speciesId];
    }

    private WhosThatPokemonPanelViewModel BuildViewModel(TimeSpan remainingTime, bool isRevealed) =>
        new()
        {
            Culture = Context.Culture,
            Pokemon = _currentPokemon,
            IsRevealed = isRevealed,
            Scores = Scores,
            CurrentTurn = CurrentTurn,
            TurnsCount = TurnsCount,
            RemainingTime = remainingTime
        };
}
