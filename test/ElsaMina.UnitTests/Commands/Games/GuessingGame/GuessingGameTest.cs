using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.GuessingGame;

public class GuessingGameTest
{
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IClockService _clockService;
    private IContext _context;
    private TestGuessingGame _game;

    private static readonly DateTimeOffset BASE_TIME = new(2024, 1, 1, 12, 0, 0, offset: TimeSpan.Zero);

    [SetUp]
    public void SetUp()
    {
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _clockService = Substitute.For<IClockService>();
        _context = Substitute.For<IContext>();

        _configuration.Name.Returns("ElsaBot");
        _clockService.CurrentUtcDateTimeOffset.Returns(BASE_TIME);

        _game = new TestGuessingGame(_templatesManager, _configuration, _clockService);
        _game.Context = _context;
        _game.SetValidAnswers(["pikachu", "pichu"]);
    }

    [Test]
    public void Test_OnAnswer_ShouldIgnore_WhenSenderIsBot()
    {
        _game.OnAnswer("ElsaBot", "pikachu");

        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public void Test_OnAnswer_ShouldIgnore_WhenAnswerIsWrong()
    {
        _game.OnAnswer("Player1", "charizard");

        _context.DidNotReceive().ReplyLocalizedMessage("guessing_game_round_won", Arg.Any<object[]>());
    }

    [Test]
    public void Test_OnAnswer_ShouldIncrementScore_WhenAnswerIsCorrect()
    {
        _game.OnAnswer("Player1", "pikachu");

        _context.Received(1).ReplyLocalizedMessage("guessing_game_round_won", "Player1", 1, string.Empty);
    }

    [Test]
    public void Test_OnAnswer_ShouldIgnoreSubsequentAnswers_WhenRoundAlreadyWon()
    {
        _game.OnAnswer("Player1", "pikachu");
        _game.OnAnswer("Player2", "pikachu");

        _context.Received(1).ReplyLocalizedMessage(Arg.Is("guessing_game_round_won"), Arg.Any<object[]>());
    }

    [Test]
    public void Test_OnAnswer_ShouldIgnore_WhenSameUserAnswersTwiceWithinRateLimit()
    {
        _game.SetHasCooldown(true);
        _game.OnAnswer("Player1", "charizard"); // first attempt (wrong, records time)
        _game.OnAnswer("Player1", "pikachu");   // second attempt within 2s → rate-limited

        _context.DidNotReceive().ReplyLocalizedMessage("guessing_game_round_won", Arg.Any<object[]>());
    }

    [Test]
    public void Test_OnAnswer_ShouldSendRateLimitPm_WhenSameUserAnswersTwiceWithinRateLimit()
    {
        _game.SetHasCooldown(true);
        _context.RoomId.Returns("testroom");
        _context.GetString("guessing_game_answer_rate_limit").Returns("You are answering too fast!");

        _game.OnAnswer("Player1", "charizard"); // first attempt (wrong, records time)
        _game.OnAnswer("Player1", "pikachu");   // second attempt within 2s → rate-limited

        _context.Received(1).GetString("guessing_game_answer_rate_limit");
        _context.Received(1).SendMessageIn("testroom", "/pm player1, You are answering too fast!");
    }

    [Test]
    public void Test_OnAnswer_ShouldAllow_WhenSameUserAnswersAfterRateLimit()
    {
        _game.SetHasCooldown(true);
        _game.OnAnswer("Player1", "charizard"); // first attempt (wrong, records time)
        _clockService.CurrentUtcDateTimeOffset.Returns(BASE_TIME.AddSeconds(3));
        _game.OnAnswer("Player1", "pikachu");   // second attempt after 3s → allowed

        _context.Received(1).ReplyLocalizedMessage("guessing_game_round_won", "Player1", 1, string.Empty);
    }

    [Test]
    public void Test_OnAnswer_ShouldNotRateLimit_WhenHasCooldownIsFalse()
    {
        // HasCooldown defaults to false
        _game.OnAnswer("Player1", "charizard"); // first attempt (wrong)
        _game.OnAnswer("Player1", "pikachu");   // second attempt immediately → should be allowed

        _context.Received(1).ReplyLocalizedMessage("guessing_game_round_won", "Player1", 1, string.Empty);
    }

    [Test]
    public void Test_OnAnswer_ShouldNotSendRateLimitPm_WhenHasCooldownIsFalse()
    {
        // HasCooldown defaults to false
        _game.OnAnswer("Player1", "charizard");
        _game.OnAnswer("Player1", "pikachu");

        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Is<string>(s => s.Contains("pm")));
    }

    [Test]
    public void Test_OnAnswer_ShouldAllowAutocorrect_WhenAnswerIsLongWithOneTypo()
    {
        _game.SetValidAnswers(["bulbasaur"]);

        _game.OnAnswer("Player1", "bulbasour"); // 1 typo on 9-char word

        _context.Received(1).ReplyLocalizedMessage("guessing_game_round_won", "Player1", 1, string.Empty);
    }

    [Test]
    public void Test_OnAnswer_ShouldNotAllowAutocorrect_WhenAnswerIsShortWithOneTypo()
    {
        _game.SetValidAnswers(["eevee"]);

        _game.OnAnswer("Player1", "eevue"); // 1 typo on 5-char word → exact match required

        _context.DidNotReceive().ReplyLocalizedMessage("guessing_game_round_won", Arg.Any<object[]>());
    }

    private class TestGuessingGame : ElsaMina.Commands.Games.GuessingGame.GuessingGame
    {
        public TestGuessingGame(ITemplatesManager templatesManager, IConfiguration configuration,
            IClockService clockService)
            : base(templatesManager, configuration, clockService)
        {
        }

        public override string Identifier => "test";

        private bool _hasCooldown;
        protected override bool HasCooldown => _hasCooldown;
        public void SetHasCooldown(bool value) => _hasCooldown = value;

        public void SetValidAnswers(IEnumerable<string> answers)
        {
            CurrentValidAnswers = answers;
        }

        protected override void OnGameStart() { }
        protected override Task OnTurnStart() => Task.CompletedTask;
    }
}
