using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Games.GuessingGame.Trivia;

public class TriviaGame : GuessingGame
{
    private const string MULTIPLE_CHOICE_TEMPLATE_PATH = "Games/GuessingGame/Trivia/TriviaMultipleChoicePanel";
    private const string BOOLEAN_TEMPLATE_PATH = "Games/GuessingGame/Trivia/TriviaBooleanPanel";

    private static int NextGameId { get; set; } = 1;

    private readonly ITriviaService _triviaService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly int _gameId;

    private IReadOnlyList<TriviaQuestion> _questions = [];
    private TriviaQuestion _currentQuestion;

    public TriviaGame(ITriviaService triviaService,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IClockService clockService) : base(templatesManager, configuration, clockService)
    {
        _triviaService = triviaService;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _gameId = NextGameId++;
    }

    public override string Identifier => nameof(TriviaGame);

    protected override bool HasCooldown => true;

    private string HtmlId => $"trivia-{_gameId}-t{CurrentTurn}";

    private string CurrentTemplatePath => _currentQuestion?.Type == TriviaQuestionType.Boolean
        ? BOOLEAN_TEMPLATE_PATH
        : MULTIPLE_CHOICE_TEMPLATE_PATH;

    protected override void OnGameStart()
    {
        Context.ReplyLocalizedMessage("trivia_start");
    }

    protected override async Task OnTurnStart()
    {
        if (_questions.Count == 0)
        {
            _questions = await _triviaService.GetQuestionsAsync(TurnsCount);
            if (_questions == null || _questions.Count == 0)
            {
                Context.ReplyLocalizedMessage("trivia_fetch_error");
                StopGame();
                return;
            }

            if (_questions.Count < TurnsCount)
            {
                TurnsCount = _questions.Count;
            }
        }

        var questionIndex = Math.Max(0, CurrentTurn - 1);
        if (questionIndex >= _questions.Count)
        {
            StopGame();
            return;
        }

        _currentQuestion = _questions[questionIndex];
        CurrentValidAnswers = _currentQuestion.ValidAnswers;

        var template = await _templatesManager.GetTemplateAsync(CurrentTemplatePath,
            BuildViewModel(DEFAULT_TURN_COOLDOWN, showCorrectAnswer: false));
        Context.SendUpdatableHtml(HtmlId, template.RemoveNewlines(), isChanging: false);
    }

    protected override void OnTimerCountdown(TimeSpan remainingTime)
    {
        base.OnTimerCountdown(remainingTime);

        if (remainingTime <= TimeSpan.Zero && !HasRoundBeenWon && _currentQuestion != null)
        {
            RevealAnswer();
        }
    }

    protected override void OnCorrectAnswer()
    {
        base.OnCorrectAnswer();
        RevealAnswer();
    }

    private void RevealAnswer()
    {
        if (_currentQuestion == null)
        {
            return;
        }

        var template = _templatesManager
            .GetTemplateAsync(CurrentTemplatePath, BuildViewModel(TimeSpan.Zero, showCorrectAnswer: true))
            .Result;
        Context.SendUpdatableHtml(HtmlId, template.RemoveNewlines(), isChanging: true);
    }

    private TriviaGamePanelViewModel BuildViewModel(TimeSpan remainingTime, bool showCorrectAnswer) =>
        new()
        {
            Culture = Context.Culture,
            Question = _currentQuestion.Question,
            Category = _currentQuestion.Category,
            Difficulty = _currentQuestion.Difficulty,
            Type = _currentQuestion.Type,
            Options = _currentQuestion.Options,
            CorrectAnswer = _currentQuestion.CorrectAnswer,
            ShowCorrectAnswer = showCorrectAnswer,
            Scores = Scores,
            CurrentTurn = CurrentTurn,
            TurnsCount = TurnsCount,
            RemainingTime = remainingTime,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = Context.RoomId
        };
}
