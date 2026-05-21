using System.Globalization;
using ElsaMina.Commands.Games.RockPaperScissors;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.RockPaperScissors;

[TestFixture]
public class RpsGameTest
{
    private RpsGame _game;
    private IConfiguration _mockConfiguration;
    private ISystemService _mockSystemService;
    private ITemplatesManager _mockTemplatesManager;
    private IContext _mockContext;

    [SetUp]
    public void SetUp()
    {
        _mockConfiguration = Substitute.For<IConfiguration>();
        _mockSystemService = Substitute.For<ISystemService>();
        _mockTemplatesManager = Substitute.For<ITemplatesManager>();
        _mockContext = Substitute.For<IContext>();

        _mockConfiguration.Name.Returns("ElsaMina");
        _mockConfiguration.Trigger.Returns("-");
        _mockContext.RoomId.Returns("testroom");
        _mockContext.Culture.Returns(CultureInfo.InvariantCulture);
        _mockTemplatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult(string.Empty));
        _mockSystemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _game = new RpsGame(_mockConfiguration, _mockSystemService, _mockTemplatesManager);
        _game.Context = _mockContext;
    }

    [Test]
    public async Task Test_Join_ShouldAddFirstPlayer_WhenGameIsEmpty()
    {
        var (success, messageKey, args) = await _game.Join("Player1");

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.True);
            Assert.That(messageKey, Is.EqualTo("rps_join_success"));
            Assert.That(_game.Players, Has.Count.EqualTo(1));
            Assert.That(_game.Players[0], Is.EqualTo("Player1"));
        });
    }

    [Test]
    public async Task Test_Join_ShouldShowLobby_WhenFirstPlayerJoins()
    {
        await _game.Join("Player1");

        await _mockTemplatesManager.Received(1).GetTemplateAsync(
            "Games/RockPaperScissors/RpsLobby", Arg.Any<object>());
    }

    [Test]
    public async Task Test_Join_ShouldStartGame_WhenSecondPlayerJoins()
    {
        await _game.Join("Player1");
        await _game.Join("Player2");

        Assert.That(_game.IsStarted, Is.True);
    }

    [Test]
    public async Task Test_Join_ShouldShowChoicePanel_WhenSecondPlayerJoins()
    {
        await _game.Join("Player1");
        await _game.Join("Player2");

        await _mockTemplatesManager.Received(1).GetTemplateAsync(
            "Games/RockPaperScissors/RpsChoicePanel", Arg.Any<object>());
    }

    [Test]
    public async Task Test_Join_ShouldReturnGameFull_WhenThirdPlayerTriesToJoin()
    {
        await _game.Join("Player1");
        await _game.Join("Player2");

        var (success, messageKey, _) = await _game.Join("Player3");

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("rps_game_full"));
            Assert.That(_game.Players, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task Test_Join_ShouldReturnAlreadyJoined_WhenSamePlayerJoinsTwice()
    {
        await _game.Join("Player1");

        var (success, messageKey, _) = await _game.Join("Player1");

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("rps_already_joined"));
            Assert.That(_game.Players, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Test_Join_ShouldBeCaseInsensitive_WhenCheckingDuplicates()
    {
        await _game.Join("Player1");

        var (success, messageKey, _) = await _game.Join("PLAYER1");

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(messageKey, Is.EqualTo("rps_already_joined"));
        });
    }

    [Test]
    public async Task Test_Play_ShouldDoNothing_WhenGameNotStarted()
    {
        await _game.Join("Player1");

        await _game.Play("player1", RpsChoice.Rock);

        await _mockTemplatesManager.DidNotReceive().GetTemplateAsync(
            "Games/RockPaperScissors/RpsResult", Arg.Any<object>());
    }

    [Test]
    public async Task Test_Play_ShouldDoNothing_WhenUserIsNotAPlayer()
    {
        await _game.Join("Player1");
        await _game.Join("Player2");

        await _game.Play("outsider", RpsChoice.Rock);

        await _mockTemplatesManager.DidNotReceive().GetTemplateAsync(
            "Games/RockPaperScissors/RpsResult", Arg.Any<object>());
    }

    [Test]
    public async Task Test_Play_ShouldIgnoreDuplicateChoice_WhenPlayerChoicesTwice()
    {
        await _game.Join("Player1");
        await _game.Join("Player2");

        await _game.Play("player1", RpsChoice.Rock);
        await _game.Play("player1", RpsChoice.Paper);

        await _mockTemplatesManager.DidNotReceive().GetTemplateAsync(
            "Games/RockPaperScissors/RpsResult", Arg.Any<object>());
        Assert.That(_game.IsEnded, Is.False);
    }

    [Test]
    public async Task Test_Play_ShouldShowResult_WhenBothPlayersChoose()
    {
        await _game.Join("Player1");
        await _game.Join("Player2");

        await _game.Play("player1", RpsChoice.Rock);
        await _game.Play("player2", RpsChoice.Scissors);

        await _mockTemplatesManager.Received(1).GetTemplateAsync(
            "Games/RockPaperScissors/RpsResult", Arg.Any<object>());
        Assert.That(_game.IsEnded, Is.True);
    }

    [Test]
    public async Task Test_Cancel_ShouldEndGame()
    {
        await _game.Join("Player1");

        _game.Cancel();

        Assert.That(_game.IsEnded, Is.True);
    }

    [Test]
    public async Task Test_Cancel_ShouldClearHtmlPanel()
    {
        await _game.Join("Player1");

        _game.Cancel();

        _mockContext.Received(1).SendUpdatableHtml(Arg.Any<string>(), string.Empty, true);
    }

    [Test]
    public void Test_Beats_ShouldReturnTrue_WhenRockBeatsScissors()
    {
        Assert.That(RpsGame.Beats(RpsChoice.Rock, RpsChoice.Scissors), Is.True);
    }

    [Test]
    public void Test_Beats_ShouldReturnTrue_WhenScissorsBeatsPaper()
    {
        Assert.That(RpsGame.Beats(RpsChoice.Scissors, RpsChoice.Paper), Is.True);
    }

    [Test]
    public void Test_Beats_ShouldReturnTrue_WhenPaperBeatsRock()
    {
        Assert.That(RpsGame.Beats(RpsChoice.Paper, RpsChoice.Rock), Is.True);
    }

    [Test]
    public void Test_Beats_ShouldReturnFalse_WhenScissorsVsRock()
    {
        Assert.That(RpsGame.Beats(RpsChoice.Scissors, RpsChoice.Rock), Is.False);
    }

    [Test]
    public void Test_Beats_ShouldReturnFalse_WhenSameChoice()
    {
        Assert.That(RpsGame.Beats(RpsChoice.Rock, RpsChoice.Rock), Is.False);
    }

    [TestCase(RpsChoice.Rock, "✊")]
    [TestCase(RpsChoice.Paper, "📄")]
    [TestCase(RpsChoice.Scissors, "✂️")]
    public void Test_ChoiceEmoji_ShouldReturnCorrectEmoji_ForEachChoice(RpsChoice choice, string expectedEmoji)
    {
        Assert.That(RpsGame.ChoiceEmoji(choice), Is.EqualTo(expectedEmoji));
    }
}
