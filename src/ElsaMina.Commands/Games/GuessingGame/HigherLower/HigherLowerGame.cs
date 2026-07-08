using System.Globalization;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.GuessingGame.HigherLower;

public class HigherLowerGame : GuessingGame
{
    private const string TEMPLATE_PATH = "Games/GuessingGame/HigherLower/HigherLowerPanel";
    private const int MAX_PAIR_ATTEMPTS = 20;

    // ASCII-only synonyms across the supported locales (accents are stripped before matching).
    private static readonly IReadOnlyList<string> HIGHER_ANSWERS =
        ["higher", "high", "up", "more", "plus", "haut", "mas", "mayor", "alto", "mais", "hoch", "h"];

    private static readonly IReadOnlyList<string> LOWER_ANSWERS =
        ["lower", "low", "down", "less", "moins", "bas", "menos", "menor", "bajo", "basso", "tief", "l"];

    private static readonly IReadOnlyList<HigherLowerCategory> CATEGORIES = BuildCategories();

    private static int NextGameId { get; set; } = 1;

    private readonly IDexManager _dexManager;
    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly int _gameId;

    private Pokemon _pokemonA;
    private Pokemon _pokemonB;
    private HigherLowerCategory _category;
    private double _valueA;
    private double _valueB;

    public HigherLowerGame(ITemplatesManager templatesManager, IConfiguration configuration,
        IDexManager dexManager, IRandomService randomService, IClockService clockService)
        : base(templatesManager, configuration, clockService)
    {
        _templatesManager = templatesManager;
        _configuration = configuration;
        _dexManager = dexManager;
        _randomService = randomService;

        _gameId = NextGameId++;
    }

    public override string Identifier => nameof(HigherLowerGame);

    protected override bool HasCooldown => true;

    private string HtmlId => $"higherlower-{_gameId}-t{CurrentTurn}";

    private bool IsHigher => _valueB > _valueA;

    protected override void OnGameStart()
    {
        Context.ReplyLocalizedMessage("higherlower_start");
    }

    protected override async Task OnTurnStart()
    {
        var pool = _dexManager.Pokedex
            .Where(pokemon => pokemon.PokedexId > 0 && pokemon.Stats != null)
            .ToList();

        for (var attempt = 0; attempt < MAX_PAIR_ATTEMPTS; attempt++)
        {
            var category = _randomService.RandomElement(CATEGORIES);
            var first = _randomService.RandomElement(pool);
            var second = _randomService.RandomElement(pool);

            var firstValue = category.ValueSelector(first);
            var secondValue = category.ValueSelector(second);

            if (first.PokedexId == second.PokedexId
                || firstValue is null || secondValue is null
                || Math.Abs(firstValue.Value - secondValue.Value) < double.Epsilon)
            {
                continue;
            }

            _category = category;
            _pokemonA = first;
            _pokemonB = second;
            _valueA = firstValue.Value;
            _valueB = secondValue.Value;
            break;
        }

        CurrentValidAnswers = IsHigher ? HIGHER_ANSWERS : LOWER_ANSWERS;

        var template = await _templatesManager.GetTemplateAsync(TEMPLATE_PATH,
            BuildViewModel(DEFAULT_TURN_COOLDOWN, isRevealed: false));
        Context.SendUpdatableHtml(HtmlId, template.RemoveNewlines(), isChanging: false);
    }

    protected override void OnCorrectAnswer()
    {
        base.OnCorrectAnswer();
        RevealPanel();
    }

    protected override void OnTimerCountdown(TimeSpan remainingTime)
    {
        base.OnTimerCountdown(remainingTime);
        if (remainingTime <= TimeSpan.Zero && !HasRoundBeenWon)
        {
            RevealPanel();
        }
    }

    private void RevealPanel()
    {
        var template = _templatesManager
            .GetTemplateAsync(TEMPLATE_PATH, BuildViewModel(TimeSpan.Zero, isRevealed: true))
            .Result;
        Context.SendUpdatableHtml(HtmlId, template.RemoveNewlines(), isChanging: true);
    }

    private HigherLowerPanelViewModel BuildViewModel(TimeSpan remainingTime, bool isRevealed) =>
        new()
        {
            Culture = Context.Culture,
            CategoryLabelKey = _category.LabelKey,
            PokemonA = _pokemonA,
            PokemonB = _pokemonB,
            ValueADisplay = _category.Formatter(_valueA),
            ValueBDisplay = _category.Formatter(_valueB),
            IsRevealed = isRevealed,
            IsHigher = IsHigher,
            Scores = Scores,
            CurrentTurn = CurrentTurn,
            TurnsCount = TurnsCount,
            RemainingTime = remainingTime,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = Context.RoomId
        };

    private static IReadOnlyList<HigherLowerCategory> BuildCategories()
    {
        static string FormatInteger(double value) => value.ToString("0", CultureInfo.InvariantCulture);
        static string FormatWeight(double value) => $"{value.ToString("0.#", CultureInfo.InvariantCulture)} kg";
        static string FormatHeight(double value) => $"{value.ToString("0.#", CultureInfo.InvariantCulture)} m";

        return
        [
            new("higherlower_category_bst", GetBaseStatTotal, FormatInteger),
            new("higherlower_category_hp", pokemon => pokemon.Stats?.HP, FormatInteger),
            new("higherlower_category_atk", pokemon => pokemon.Stats?.Attack, FormatInteger),
            new("higherlower_category_def", pokemon => pokemon.Stats?.Defense, FormatInteger),
            new("higherlower_category_spa", pokemon => pokemon.Stats?.SpecialAttack, FormatInteger),
            new("higherlower_category_spd", pokemon => pokemon.Stats?.SpecialDefense, FormatInteger),
            new("higherlower_category_spe", pokemon => pokemon.Stats?.Speed, FormatInteger),
            new("higherlower_category_weight", pokemon => ParseMeasurement(pokemon.Weight), FormatWeight),
            new("higherlower_category_height", pokemon => ParseMeasurement(pokemon.Height), FormatHeight),
            new("higherlower_category_dex", pokemon => pokemon.PokedexId, FormatInteger)
        ];
    }

    private static double? GetBaseStatTotal(Pokemon pokemon)
    {
        var stats = pokemon.Stats;
        if (stats == null)
        {
            return null;
        }

        return stats.HP + stats.Attack + stats.Defense + stats.SpecialAttack + stats.SpecialDefense + stats.Speed;
    }

    private static double? ParseMeasurement(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var numericPart = raw.Trim().Split(' ')[0].Replace(',', '.');
        return double.TryParse(numericPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
