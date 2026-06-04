using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Games.Semantix;

public class SemantixGame : Game, ISemantixGame
{
    private static int NextGameId { get; set; } = 1;

    private readonly IEmbeddingService _embeddingService;
    private readonly ISemantixDailyService _dailyService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IBotDbContextFactory _dbContextFactory;

    private readonly int _gameId;
    private readonly List<SemantixGuess> _guesses = [];
    private readonly PeriodicTimerRunner _inactivityTimer;
    private float[] _targetVector;
    private int _privateRenderCount;
    private bool _publicInitialized;
    private bool _resultSaved;

    public SemantixGame(IEmbeddingService embeddingService,
        ISemantixDailyService dailyService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IBotDbContextFactory dbContextFactory)
    {
        _embeddingService = embeddingService;
        _dailyService = dailyService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _inactivityTimer = new PeriodicTimerRunner(SemantixConstants.INACTIVITY_TIMEOUT, OnInactivityTimeout,
            runOnce: true);

        _gameId = NextGameId++;
    }

    public override string Identifier => nameof(SemantixGame);

    public bool IsRoundActive { get; private set; }
    public bool IsWon { get; private set; }
    public string Answer { get; private set; }
    public IReadOnlyList<SemantixGuess> Guesses => _guesses;
    public SemantixGuess LastGuess { get; private set; }
    public bool IsPrivateMode { get; set; }
    public string TargetRoomId { get; set; }
    public string TargetUserId { get; set; }
    public IContext Context { get; set; }
    public IUser Owner { get; set; }

    private string EffectiveRoomId => IsPrivateMode ? TargetRoomId : Context.RoomId;
    // A fresh id on each private render so the board re-appears at the bottom of the PM
    // instead of updating in place higher up in the conversation.
    private string PrivatePanelId => $"sx-{EffectiveRoomId}-{_gameId}-{_privateRenderCount}";
    private string PublicPanelId => $"sx-pub-{EffectiveRoomId}-{_gameId}";

    public async Task<bool> StartNewRound()
    {
        Answer = _dailyService.GetDailyAnswer();
        if (string.IsNullOrEmpty(Answer))
        {
            Log.Error("Semantix daily answer is unavailable.");
            Context.ReplyLocalizedMessage("sx_game_unavailable");
            return false;
        }

        _targetVector = await _embeddingService.GetEmbeddingAsync(Answer);
        if (_targetVector == null)
        {
            Context.ReplyLocalizedMessage("sx_game_api_unavailable");
            return false;
        }

        _guesses.Clear();
        LastGuess = null;
        IsWon = false;
        IsRoundActive = true;

        if (!IsStarted)
        {
            OnStart();
        }

        _inactivityTimer.Restart();
        Context.ReplyLocalizedMessage("sx_game_started");
        await RenderPublicAsync();
        await RenderPrivateAsync();
        return true;
    }

    public async Task ResumeAsync()
    {
        _inactivityTimer.Restart();
        await RenderPublicAsync();
        await RenderPrivateAsync();
    }

    public async Task<SemantixGuessOutcome> SubmitGuess(IUser user, string word)
    {
        if (!IsRoundActive)
        {
            return SemantixGuessOutcome.RoundNotActive;
        }

        if (user.UserId != Owner?.UserId)
        {
            return SemantixGuessOutcome.NotOwner;
        }

        var guess = (word ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(guess))
        {
            return await RejectAsync(SemantixGuessOutcome.EmptyWord);
        }

        if (!_dailyService.IsValidWord(guess))
        {
            return await RejectAsync(SemantixGuessOutcome.NotInWordList);
        }

        if (_guesses.Any(existing => existing.Word == guess))
        {
            return await RejectAsync(SemantixGuessOutcome.AlreadyGuessed);
        }

        _inactivityTimer.Restart();

        if (guess == Answer)
        {
            var winningGuess = new SemantixGuess
            {
                Word = guess,
                Similarity = 1,
                Temperature = SemantixConstants.WIN_TEMPERATURE,
                Order = _guesses.Count + 1
            };
            _guesses.Add(winningGuess);
            LastGuess = winningGuess;

            IsWon = true;
            IsRoundActive = false;
            Context.ReplyLocalizedMessage("sx_game_win", Owner.Name, _guesses.Count);
            await SaveResultAsync();
            _inactivityTimer.Stop();
            OnEnd();
            await RenderPublicAsync();
            await RenderPrivateAsync();
            return SemantixGuessOutcome.Won;
        }

        var guessVector = await _embeddingService.GetEmbeddingAsync(guess);
        if (guessVector == null)
        {
            return await RejectAsync(SemantixGuessOutcome.EmbeddingUnavailable);
        }

        var similarity = SemantixMath.CosineSimilarity(guessVector, _targetVector);
        var temperature = SemantixMath.ToTemperature(similarity);

        // Raw similarity logged to help calibrate the temperature curve from real data.
        Log.Information("Semantix guess '{0}' vs answer: similarity={1:0.0000} temperature={2}",
            guess, similarity, temperature);

        var newGuess = new SemantixGuess
        {
            Word = guess,
            Similarity = similarity,
            Temperature = temperature,
            Order = _guesses.Count + 1
        };
        _guesses.Add(newGuess);
        LastGuess = newGuess;

        await RenderPublicAsync();
        await RenderPrivateAsync();
        return SemantixGuessOutcome.Accepted;
    }

    public async Task CancelAsync()
    {
        IsRoundActive = false;
        _inactivityTimer.Stop();
        OnEnd();
        await RenderPublicAsync();
        await RenderPrivateAsync();
    }

    /// <summary>
    /// Re-renders the private board on a rejected guess so the submission form
    /// resets (the client locks a form on "Submitted!" until its HTML is updated).
    /// </summary>
    private async Task<SemantixGuessOutcome> RejectAsync(SemantixGuessOutcome outcome)
    {
        await RenderPrivateAsync();
        return outcome;
    }

    private async Task OnInactivityTimeout()
    {
        if (IsEnded)
        {
            return;
        }

        Context.ReplyLocalizedMessage("sx_game_timeout");
        await CancelAsync();
    }

    private async Task SaveResultAsync()
    {
        if (Owner == null || _resultSaved)
        {
            return;
        }

        _resultSaved = true;
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var record = await dbContext.SemantixScores.FindAsync(Owner.UserId);
            if (record == null)
            {
                await dbContext.EnsureUserExistsAsync(Owner.UserId);
                record = new SemantixScore { UserId = Owner.UserId };
                await dbContext.SemantixScores.AddAsync(record);
            }

            var today = _dailyService.Today;
            var playedYesterday = record.LastWonDate == today.AddDays(-1);

            record.GamesPlayed++;
            record.Wins++;
            record.TotalGuesses += _guesses.Count;
            record.CurrentStreak = playedYesterday ? record.CurrentStreak + 1 : 1;
            record.MaxStreak = Math.Max(record.MaxStreak, record.CurrentStreak);
            if (record.BestGuessCount == 0 || _guesses.Count < record.BestGuessCount)
            {
                record.BestGuessCount = _guesses.Count;
            }

            record.LastWonDate = today;
            await dbContext.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to save semantix result");
        }
    }

    private SemantixModel BuildModel(bool showAnswer)
    {
        return new SemantixModel
        {
            Culture = Context.Culture,
            CurrentGame = this,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = EffectiveRoomId,
            IsPrivateMode = IsPrivateMode,
            ShowAnswer = showAnswer
        };
    }

    private async Task RenderPrivateAsync()
    {
        if (Owner == null)
        {
            return;
        }

        // Use a fresh id each render and always "send" (not "change") so the board
        // re-appears at the bottom of the PM instead of updating in place above.
        _privateRenderCount++;
        var template = await _templatesManager.GetTemplateAsync("Games/Semantix/SemantixBoard",
            BuildModel(showAnswer: IsWon));
        Context.SendPrivateUpdatableHtml(Owner.UserId, EffectiveRoomId, PrivatePanelId,
            template.RemoveNewlines(), isChanging: false);
    }

    private async Task RenderPublicAsync()
    {
        if (IsPrivateMode || Owner == null)
        {
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("Games/Semantix/SemantixPublic",
            BuildModel(showAnswer: false));
        Context.SendUpdatableHtml(PublicPanelId, template.RemoveNewlines(), _publicInitialized);
        _publicInitialized = true;
    }
}
