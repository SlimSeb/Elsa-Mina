using System.Globalization;
using System.Text;
using System.Threading;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using JetBrains.Annotations;

namespace ElsaMina.Commands.Games.Scattergories;

public class ScattergoriesGame : Game, IScattergoriesGame
{
    private static int _nextGameId;

    private readonly IDexManager _dexManager;
    private readonly IRandomService _randomService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly ISystemService _systemService;

    private readonly Lock _roundLock = new();
    private readonly Dictionary<ScattergoriesPlayer, int> _scores = new();

    // Rebuilt every round: normalized localized name -> Pokémon it belongs to.
    private readonly Dictionary<string, Pokemon> _roundAnswers = new();
    private readonly HashSet<int> _foundIds = new();
    private readonly List<ScattergoriesFoundPokemon> _found = new();
    private readonly HashSet<char> _usedLetters = new();

    private PeriodicTimerRunner _timer;
    private int _elapsedSeconds;
    private char _letter;
    private int _eligibleCount;

    [UsedImplicitly]
    public ScattergoriesGame(IDexManager dexManager,
        IRandomService randomService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        ISystemService systemService)
    {
        _dexManager = dexManager;
        _randomService = randomService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _systemService = systemService;
        GameId = Interlocked.Increment(ref _nextGameId);
    }

    public int GameId { get; }
    public IContext Context { get; set; }
    public override string Identifier => nameof(ScattergoriesGame);

    private int _roundNumber;

    private string HtmlId => $"scattergories-{GameId}-r{_roundNumber}";

    public async Task StartAsync()
    {
        OnStart();
        Context.ReplyLocalizedMessage("scattergories_start", ScattergoriesConstants.ROUNDS_COUNT,
            (int)ScattergoriesConstants.ROUND_DURATION.TotalSeconds);
        await InitializeNextRoundAsync();
    }

    public void Cancel()
    {
        CancelTimer();
        OnEnd();
    }

    private async Task InitializeNextRoundAsync()
    {
        _roundNumber++;

        lock (_roundLock)
        {
            _roundAnswers.Clear();
            _foundIds.Clear();
            _found.Clear();
            _letter = PickLetter();
            _usedLetters.Add(_letter);
            _eligibleCount = BuildRoundAnswers(_letter);
        }

        var html = await BuildRoundHtmlAsync(isRoundOver: false);
        Context.SendUpdatableHtml(HtmlId, html, isChanging: false);

        _elapsedSeconds = 0;
        CancelTimer();
        _timer = new PeriodicTimerRunner(TimeSpan.FromSeconds(1), HandleTimerTickAsync);
        _timer.Start();
    }

    private async Task HandleTimerTickAsync()
    {
        _elapsedSeconds++;
        var remaining = ScattergoriesConstants.ROUND_DURATION - TimeSpan.FromSeconds(_elapsedSeconds);

        if (remaining == ScattergoriesConstants.WARNING_THRESHOLD)
        {
            Context.ReplyLocalizedMessage("scattergories_time_warning", (int)remaining.TotalSeconds,
                char.ToUpperInvariant(_letter));
            return;
        }

        if (remaining > TimeSpan.Zero)
        {
            return;
        }

        CancelTimer();
        await EndRoundAsync();
    }

    private async Task EndRoundAsync()
    {
        var html = await BuildRoundHtmlAsync(isRoundOver: true);
        Context.SendUpdatableHtml(HtmlId, html, isChanging: true);

        if (_roundNumber >= ScattergoriesConstants.ROUNDS_COUNT || IsEnded)
        {
            await EndGameAsync();
            return;
        }

        await _systemService.SleepAsync(ScattergoriesConstants.INTER_ROUND_DELAY);
        if (!IsEnded)
        {
            await InitializeNextRoundAsync();
        }
    }

    public void OnAnswer(string userName, string answer)
    {
        if (IsEnded || string.IsNullOrWhiteSpace(answer))
        {
            return;
        }

        if (userName.ToLowerAlphaNum() == _configuration.Name.ToLowerAlphaNum())
        {
            return;
        }

        var normalized = Normalize(answer);
        if (normalized.Length == 0)
        {
            return;
        }

        ScattergoriesFoundPokemon foundEntry = null;
        int newScore = 0;
        lock (_roundLock)
        {
            if (!_roundAnswers.TryGetValue(normalized, out var pokemon) || _foundIds.Contains(pokemon.PokedexId))
            {
                return;
            }

            _foundIds.Add(pokemon.PokedexId);

            var player = new ScattergoriesPlayer(userName.ToLowerAlphaNum(), userName);
            _scores.TryAdd(player, 0);
            _scores[player] += 1;
            newScore = _scores[player];

            var displayName = pokemon.Name?.English ?? pokemon.Name?.French ?? normalized;
            foundEntry = new ScattergoriesFoundPokemon(displayName, pokemon.Sprites?.Regular, userName);
            _found.Add(foundEntry);
        }

        Context.ReplyLocalizedMessage("scattergories_answer_scored", userName, foundEntry.DisplayName, newScore);
        _ = RefreshRoundHtmlAsync();
    }

    private async Task RefreshRoundHtmlAsync()
    {
        if (IsEnded)
        {
            return;
        }

        var html = await BuildRoundHtmlAsync(isRoundOver: false);
        Context.SendUpdatableHtml(HtmlId, html, isChanging: true);
    }

    private async Task EndGameAsync()
    {
        CancelTimer();
        OnEnd();

        var model = new ScattergoriesResultModel
        {
            Culture = Context.Culture,
            TotalRounds = ScattergoriesConstants.ROUNDS_COUNT,
            Scores = GetOrderedScores()
        };
        var html = (await _templatesManager.GetTemplateAsync("Games/Scattergories/ScattergoriesResult", model))
            .RemoveNewlines();
        Context.ReplyHtml(html);
    }

    private char PickLetter()
    {
        var candidates = BuildLetterPool();
        var available = candidates.Where(letter => !_usedLetters.Contains(letter)).ToList();
        if (available.Count == 0)
        {
            available = candidates;
        }

        return available.Count > 0 ? _randomService.RandomElement(available) : 'p';
    }

    private List<char> BuildLetterPool()
    {
        var counts = new Dictionary<char, int>();
        foreach (var pokemon in EnumeratePokedex())
        {
            var letters = new HashSet<char>();
            foreach (var name in LocalizedNames(pokemon))
            {
                var normalized = Normalize(name);
                if (normalized.Length > 0)
                {
                    letters.Add(normalized[0]);
                }
            }

            foreach (var letter in letters)
            {
                counts[letter] = counts.GetValueOrDefault(letter) + 1;
            }
        }

        return counts
            .Where(entry => entry.Key is >= 'a' and <= 'z'
                            && entry.Value >= ScattergoriesConstants.MIN_POKEMON_PER_LETTER)
            .Select(entry => entry.Key)
            .ToList();
    }

    private int BuildRoundAnswers(char letter)
    {
        var eligibleIds = new HashSet<int>();
        foreach (var pokemon in EnumeratePokedex())
        {
            foreach (var name in LocalizedNames(pokemon))
            {
                var normalized = Normalize(name);
                if (normalized.Length == 0 || normalized[0] != letter)
                {
                    continue;
                }

                _roundAnswers[normalized] = pokemon;
                eligibleIds.Add(pokemon.PokedexId);
            }
        }

        return eligibleIds.Count;
    }

    private IEnumerable<Pokemon> EnumeratePokedex()
    {
        foreach (var pokemon in _dexManager.Pokedex)
        {
            if (pokemon?.Name is null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(pokemon.Name.English) && string.IsNullOrEmpty(pokemon.Name.French))
            {
                continue;
            }

            yield return pokemon;
        }
    }

    private static IEnumerable<string> LocalizedNames(Pokemon pokemon)
    {
        if (!string.IsNullOrEmpty(pokemon.Name.English))
        {
            yield return pokemon.Name.English;
        }

        if (!string.IsNullOrEmpty(pokemon.Name.French))
        {
            yield return pokemon.Name.French;
        }
    }

    private IReadOnlyList<(string Name, int Points)> GetOrderedScores()
    {
        lock (_roundLock)
        {
            return _scores
                .OrderByDescending(entry => entry.Value)
                .Select(entry => (entry.Key.UserName, entry.Value))
                .ToList();
        }
    }

    private async Task<string> BuildRoundHtmlAsync(bool isRoundOver)
    {
        ScattergoriesModel model;
        lock (_roundLock)
        {
            model = new ScattergoriesModel
            {
                Culture = Context.Culture,
                RoundNumber = _roundNumber,
                TotalRounds = ScattergoriesConstants.ROUNDS_COUNT,
                Letter = char.ToUpperInvariant(_letter),
                RoundDurationSeconds = (int)ScattergoriesConstants.ROUND_DURATION.TotalSeconds,
                IsRoundOver = isRoundOver,
                EligibleCount = _eligibleCount,
                Found = _found.ToList(),
                Scores = _scores
                    .OrderByDescending(entry => entry.Value)
                    .Select(entry => (entry.Key.UserName, entry.Value))
                    .ToList()
            };
        }

        return (await _templatesManager.GetTemplateAsync("Games/Scattergories/ScattergoriesRound", model))
            .RemoveNewlines();
    }

    private void CancelTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// Lower-cases a name and strips diacritics and punctuation so that "Évoli", "Farfetch'd" or
    /// "Mr. Mime" all reduce to a comparable ASCII token. <see cref="StringExtensions.ToLowerAlphaNum"/>
    /// cannot be used because it drops accented letters entirely, which corrupts French names.
    /// </summary>
    private static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }
}
