using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;

namespace ElsaMina.Commands.Games.Wordle;

public class WordleGame : Game, IWordleGame
{
    private static int NextGameId { get; set; } = 1;

    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IWordleDailyService _dailyService;
    private readonly IClockService _clockService;

    private readonly int _gameId;
    private readonly PeriodicTimerRunner _inactivityTimer;
    private readonly List<WordleGuess> _guesses = [];
    private readonly Dictionary<char, WordleLetterState> _keyboardStates = new();
    private HashSet<string> _validWords = [];
    private bool _publicInitialized;
    private bool _privateInitialized;
    private bool _resultSaved;

    public WordleGame(ITemplatesManager templatesManager,
        IConfiguration configuration,
        IBotDbContextFactory dbContextFactory,
        IWordleDailyService dailyService,
        IClockService clockService)
    {
        _templatesManager = templatesManager;
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _dailyService = dailyService;
        _clockService = clockService;
        _inactivityTimer = new PeriodicTimerRunner(WordleConstants.INACTIVITY_TIMEOUT, OnInactivityTimeout, runOnce: true);

        _gameId = NextGameId++;
    }

    public override string Identifier => nameof(WordleGame);

    public IReadOnlyList<WordleGuess> Guesses => _guesses;
    public int MaxGuesses => WordleConstants.MAX_GUESSES;
    public int WordLength => WordleConstants.WORD_LENGTH;
    public bool IsRoundActive { get; private set; }
    public bool IsWon { get; private set; }
    public string Answer { get; private set; }
    public string RevealedAnswer => IsRoundActive ? null : Answer;
    public string CurrentInput { get; private set; } = string.Empty;
    public IReadOnlyDictionary<char, WordleLetterState> KeyboardStates => _keyboardStates;
    public bool IsPrivateMode { get; set; }
    public string TargetRoomId { get; set; }
    public string TargetUserId { get; set; }
    public IContext Context { get; set; }
    public IUser Owner { get; set; }

    private string EffectiveRoomId => IsPrivateMode ? TargetRoomId : Context.RoomId;
    private string PublicPanelId => $"wordle-{EffectiveRoomId}-{_gameId}";
    private string PrivatePanelId => $"wordle-panel-{EffectiveRoomId}-{_gameId}";

    public async Task StartNewRound()
    {
        _validWords = _dailyService.GetWords(Context.Culture)
            .Select(word => word.ToUpperInvariant())
            .ToHashSet();
        Answer = _dailyService.GetDailyAnswer(Context.Culture);
        _guesses.Clear();
        _keyboardStates.Clear();
        CurrentInput = string.Empty;
        IsWon = false;
        IsRoundActive = true;

        if (!IsStarted)
        {
            OnStart();
        }

        await MarkAttemptAsync();

        _inactivityTimer.Restart();
        Context.ReplyLocalizedMessage("wordle_game_started", WordLength, MaxGuesses);
        await RenderPublicAsync();
        await RenderPrivateAsync();
    }

    public async Task ResumeAsync()
    {
        _inactivityTimer.Restart();
        await RenderPublicAsync();
        await RenderPrivateAsync();
    }

    public async Task<WordleGuessOutcome> SubmitGuess(IUser user, string word)
    {
        if (!IsRoundActive)
        {
            return WordleGuessOutcome.RoundNotActive;
        }

        if (user.UserId != Owner?.UserId)
        {
            return WordleGuessOutcome.NotOwner;
        }

        var guess = (word ?? string.Empty).Trim().ToUpperInvariant();
        if (guess.Length != WordLength)
        {
            return WordleGuessOutcome.InvalidLength;
        }

        if (!guess.All(character => character is >= 'A' and <= 'Z'))
        {
            return WordleGuessOutcome.NotAlphabetic;
        }

        if (!_validWords.Contains(guess))
        {
            return WordleGuessOutcome.NotInWordList;
        }

        if (_guesses.Any(existing => existing.Word == guess))
        {
            return WordleGuessOutcome.AlreadyGuessed;
        }

        var states = Evaluate(guess, Answer);
        _guesses.Add(new WordleGuess { Word = guess, States = states });
        UpdateKeyboard(guess, states);
        CurrentInput = string.Empty;
        _inactivityTimer.Restart();

        if (guess == Answer)
        {
            IsWon = true;
            IsRoundActive = false;
            Context.ReplyLocalizedMessage("wordle_game_win", Owner.Name, _guesses.Count);
            await SaveResultAsync(won: true, guessCount: _guesses.Count);
            _inactivityTimer.Stop();
            OnEnd();
        }
        else if (_guesses.Count >= MaxGuesses)
        {
            IsRoundActive = false;
            Context.ReplyLocalizedMessage("wordle_game_lose", Owner.Name);
            await SaveResultAsync(won: false, guessCount: _guesses.Count);
            _inactivityTimer.Stop();
            OnEnd();
        }

        await RenderPublicAsync();
        await RenderPrivateAsync();
        return WordleGuessOutcome.Accepted;
    }

    public async Task AppendLetter(IUser user, char letter)
    {
        if (!IsRoundActive || user.UserId != Owner?.UserId)
        {
            return;
        }

        if (!char.IsLetter(letter) || CurrentInput.Length >= WordLength)
        {
            return;
        }

        CurrentInput += char.ToUpperInvariant(letter);
        _inactivityTimer.Restart();
        await RenderPrivateAsync();
    }

    public async Task RemoveLetter(IUser user)
    {
        if (!IsRoundActive || user.UserId != Owner?.UserId || CurrentInput.Length == 0)
        {
            return;
        }

        CurrentInput = CurrentInput[..^1];
        _inactivityTimer.Restart();
        await RenderPrivateAsync();
    }

    public async Task SubmitCurrentInput(IUser user)
    {
        if (!IsRoundActive || user.UserId != Owner?.UserId || CurrentInput.Length != WordLength)
        {
            return;
        }

        var outcome = await SubmitGuess(user, CurrentInput);
        switch (outcome)
        {
            case WordleGuessOutcome.NotInWordList:
                Context.ReplyLocalizedMessage("wordle_guess_not_in_list");
                break;
            case WordleGuessOutcome.AlreadyGuessed:
                Context.ReplyLocalizedMessage("wordle_guess_already_guessed");
                break;
        }
    }

    public async Task CancelAsync()
    {
        await SaveResultAsync(won: false, guessCount: _guesses.Count);
        IsRoundActive = false;
        _inactivityTimer.Stop();
        OnEnd();
        await RenderPublicAsync();
        await RenderPrivateAsync();
    }

    private static WordleLetterState[] Evaluate(string guess, string answer)
    {
        var states = new WordleLetterState[guess.Length];
        var remaining = new Dictionary<char, int>();

        for (var i = 0; i < answer.Length; i++)
        {
            if (guess[i] == answer[i])
            {
                states[i] = WordleLetterState.Correct;
            }
            else
            {
                remaining.TryGetValue(answer[i], out var count);
                remaining[answer[i]] = count + 1;
            }
        }

        for (var i = 0; i < guess.Length; i++)
        {
            if (states[i] == WordleLetterState.Correct)
            {
                continue;
            }

            if (remaining.TryGetValue(guess[i], out var count) && count > 0)
            {
                states[i] = WordleLetterState.Present;
                remaining[guess[i]] = count - 1;
            }
            else
            {
                states[i] = WordleLetterState.Absent;
            }
        }

        return states;
    }

    private void UpdateKeyboard(string guess, IReadOnlyList<WordleLetterState> states)
    {
        for (var i = 0; i < guess.Length; i++)
        {
            var letter = guess[i];
            var state = states[i];
            if (!_keyboardStates.TryGetValue(letter, out var existing) || state > existing)
            {
                _keyboardStates[letter] = state;
            }
        }
    }

    private async Task OnInactivityTimeout()
    {
        if (IsEnded)
        {
            return;
        }

        Context.ReplyLocalizedMessage("wordle_game_timeout");
        await CancelAsync();
    }

    /// <summary>
    /// Records that the player has used their attempt for the day. Called once when the game starts so a
    /// player cannot replay the daily word, even if they abandon the game before finishing.
    /// </summary>
    private async Task MarkAttemptAsync()
    {
        if (Owner == null)
        {
            return;
        }

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var record = await db.WordleScores.FindAsync(Owner.UserId);
            if (record == null)
            {
                await db.EnsureUserExistsAsync(Owner.UserId);
                record = new WordleScore { UserId = Owner.UserId };
                await db.WordleScores.AddAsync(record);
            }

            record.GamesPlayed++;
            record.LastPlayedDate = DateOnly.FromDateTime(_clockService.CurrentUtcDateTime);
            await db.SaveChangesAsync();
        }
        catch
        {
            // ignore DB errors so the game stays playable
        }
    }

    private async Task SaveResultAsync(bool won, int guessCount)
    {
        if (Owner == null || _resultSaved)
        {
            return;
        }

        _resultSaved = true;
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var record = await db.WordleScores.FindAsync(Owner.UserId);
            if (record == null)
            {
                await db.EnsureUserExistsAsync(Owner.UserId);
                record = new WordleScore
                {
                    UserId = Owner.UserId,
                    LastPlayedDate = DateOnly.FromDateTime(_clockService.CurrentUtcDateTime)
                };
                await db.WordleScores.AddAsync(record);
            }

            record.TotalGuesses += guessCount;
            if (won)
            {
                record.Wins++;
                record.CurrentStreak++;
                record.MaxStreak = Math.Max(record.MaxStreak, record.CurrentStreak);
            }
            else
            {
                record.CurrentStreak = 0;
            }

            await db.SaveChangesAsync();
        }
        catch
        {
            // ignore DB errors so the game stays playable
        }
    }

    /// <summary>
    /// Renders the grid-only spectator view in the room. Refreshed when a guess is committed,
    /// never on intermediate keystrokes, so the public box does not churn while the player types.
    /// </summary>
    private async Task RenderPublicAsync()
    {
        if (IsPrivateMode || Owner == null)
        {
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Games/Wordle/WordlePublic", BuildModel());
        Context.SendUpdatableHtml(PublicPanelId, template.RemoveNewlines(), _publicInitialized);
        _publicInitialized = true;
    }

    /// <summary>
    /// Renders the full interactive board (grid, typed letters, keyboard, options) privately to the player.
    /// </summary>
    private async Task RenderPrivateAsync()
    {
        if (Owner == null)
        {
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Games/Wordle/WordleBoard", BuildModel());
        Context.SendPrivateUpdatableHtml(Owner.UserId, EffectiveRoomId, PrivatePanelId,
            template.RemoveNewlines(), _privateInitialized);
        _privateInitialized = true;
    }

    private WordleModel BuildModel() => new()
    {
        Culture = Context.Culture,
        CurrentGame = this,
        BotName = _configuration.Name,
        Trigger = _configuration.Trigger,
        RoomId = EffectiveRoomId,
        IsPrivateMode = IsPrivateMode
    };
}
