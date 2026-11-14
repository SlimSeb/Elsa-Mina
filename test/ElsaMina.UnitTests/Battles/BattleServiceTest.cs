using ElsaMina.Battles;
using ElsaMina.Battles.Chat;
using ElsaMina.Battles.Replays;
using ElsaMina.Battles.Results;
using ElsaMina.Core;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles;

[TestFixture]
public class BattleServiceTest
{
    private const string ROOM_ID = "battle-gen9randombattle-1";
    private static readonly string[] START_PARTS = ["", "start"];
    private static readonly string[] WIN_PARTS = ["", "win", "Elsa-Mina"];
    private static readonly string[] REQUEST_PARTS = ["", "request", "{\"rqid\":1}"];
    private static readonly string[] WIN_LINES = ["|win|Elsa-Mina"];
    private static readonly string[] CRIT_PARTS = ["", "-crit", "p2a: Garchomp"];

    private IBattleMessageParser _messageParser;
    private IBot _bot;
    private ILadderingService _ladderingService;
    private IBattleResultsService _battleResultsService;
    private IReplaySavingService _replaySavingService;
    private IBattleChatService _battleChatService;
    private BattleService _battleService;

    [SetUp]
    public void SetUp()
    {
        _messageParser = Substitute.For<IBattleMessageParser>();
        _bot = Substitute.For<IBot>();
        _ladderingService = Substitute.For<ILadderingService>();
        _battleResultsService = Substitute.For<IBattleResultsService>();
        _replaySavingService = Substitute.For<IReplaySavingService>();
        _battleChatService = Substitute.For<IBattleChatService>();
        _battleService = new BattleService(_messageParser, Substitute.For<IBattleDecisionService>(), _bot,
            _ladderingService, _battleResultsService, _replaySavingService, _battleChatService);
    }

    private void SetUpBattleEnd(string sideId, BattleOutcome? outcome = BattleOutcome.Won)
    {
        _battleResultsService.GetOutcome(Arg.Any<BattleMessageResult>()).Returns(outcome);
        _messageParser.TryApplyMessage(WIN_PARTS, ROOM_ID, Arg.Any<BattleContext>(), out Arg.Any<BattleMessageResult>())
            .Returns(callInfo =>
            {
                callInfo.ArgAt<BattleContext>(2).SideId = sideId;
                callInfo[3] = new BattleMessageResult(BattleMessageType.BattleEnded, "Elsa-Mina");
                return true;
            });
    }

    private void SetUpRequest(string roomId)
    {
        _messageParser.TryApplyMessage(REQUEST_PARTS, roomId, Arg.Any<BattleContext>(), out Arg.Any<BattleMessageResult>())
            .Returns(callInfo =>
            {
                callInfo.ArgAt<BattleContext>(2).SideId = "p1";
                return false;
            });
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotifyLaddering_WhenBotPlayedTheBattle()
    {
        // Arrange
        SetUpBattleEnd("p1");

        // Act
        await _battleService.HandleMessageAsync(WIN_PARTS, ROOM_ID);

        // Assert
        _ladderingService.Received(1).OnBattleEnded();
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotNotifyLaddering_WhenBotOnlySpectatedTheBattle()
    {
        // Arrange
        SetUpBattleEnd("");

        // Act
        await _battleService.HandleMessageAsync(WIN_PARTS, ROOM_ID);

        // Assert
        _ladderingService.DidNotReceive().OnBattleEnded();
    }

    [TestCase(BattleOutcome.Won)]
    [TestCase(BattleOutcome.Lost)]
    public async Task Test_HandleMessageAsync_ShouldRecordResult_WhenBotPlayedTheBattle(BattleOutcome expected)
    {
        // Arrange
        SetUpBattleEnd("p1", expected);

        // Act
        await _battleService.HandleMessageAsync(WIN_PARTS, ROOM_ID);

        // Assert
        _battleResultsService.Received(1).OnBattleEnded(Arg.Is<BattleContext>(context => context.RoomId == ROOM_ID),
            expected);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldSaveReplayBeforeLeaving_WhenBotPlayedTheBattle()
    {
        // Arrange
        SetUpBattleEnd("p1");

        // Act
        await _battleService.HandleMessageAsync(WIN_PARTS, ROOM_ID);

        // Assert: with every line received, the |win| included
        Received.InOrder(() =>
        {
            _replaySavingService.OnBattleEndedAsync(
                Arg.Is<BattleContext>(context => context.Lines.SequenceEqual(WIN_LINES)),
                Arg.Any<CancellationToken>());
            _bot.Say(ROOM_ID, "/leave");
        });
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotRecordResult_WhenServerDidNotNameWinner()
    {
        // Arrange
        SetUpBattleEnd("p1", null);

        // Act
        await _battleService.HandleMessageAsync(WIN_PARTS, ROOM_ID);

        // Assert
        _battleResultsService.DidNotReceiveWithAnyArgs().OnBattleEnded(null, default);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotRecordNorSave_WhenBotOnlySpectatedTheBattle()
    {
        // Arrange
        SetUpBattleEnd("");

        // Act
        await _battleService.HandleMessageAsync(WIN_PARTS, ROOM_ID);

        // Assert
        _battleResultsService.DidNotReceiveWithAnyArgs().OnBattleEnded(null, default);
        await _replaySavingService.DidNotReceiveWithAnyArgs().OnBattleEndedAsync(null);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldRememberStrategy_WhenBattleStarts()
    {
        // Arrange
        _messageParser.TryApplyMessage(START_PARTS, ROOM_ID, Arg.Any<BattleContext>(), out Arg.Any<BattleMessageResult>())
            .Returns(callInfo =>
            {
                callInfo[3] = new BattleMessageResult(BattleMessageType.BattleStarted);
                return true;
            });

        // Act
        await _battleService.HandleMessageAsync(START_PARTS, ROOM_ID);

        // Assert
        _battleResultsService.Received(1).OnBattleStarted(Arg.Is<BattleContext>(context => context.RoomId == ROOM_ID));
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldSayStartMessage_WhenBattleStarts()
    {
        // Arrange
        _battleChatService.GetStartMessage().Returns("glhf!");
        _messageParser.TryApplyMessage(START_PARTS, ROOM_ID, Arg.Any<BattleContext>(), out Arg.Any<BattleMessageResult>())
            .Returns(callInfo =>
            {
                callInfo[3] = new BattleMessageResult(BattleMessageType.BattleStarted);
                return true;
            });

        // Act
        await _battleService.HandleMessageAsync(START_PARTS, ROOM_ID);

        // Assert
        _bot.Received(1).Say(ROOM_ID, "glhf!");
    }

    [TestCase(BattleOutcome.Won)]
    [TestCase(BattleOutcome.Lost)]
    [TestCase(null)]
    public async Task Test_HandleMessageAsync_ShouldSayEndMessageOfOutcome_WhenBattleEnds(BattleOutcome? outcome)
    {
        // Arrange
        SetUpBattleEnd("p1", outcome);
        _battleChatService.GetEndMessage(outcome).Returns("GG!");

        // Act
        await _battleService.HandleMessageAsync(WIN_PARTS, ROOM_ID);

        // Assert
        _bot.Received(1).Say(ROOM_ID, "GG!");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldSayReaction_WhenChatReactsToLine()
    {
        // Arrange: the parser does nothing with the line
        _battleChatService.GetReaction(CRIT_PARTS, Arg.Any<BattleContext>()).Returns("Critical hit!");

        // Act
        await _battleService.HandleMessageAsync(CRIT_PARTS, ROOM_ID);

        // Assert
        _bot.Received(1).Say(ROOM_ID, "Critical hit!");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldSayNothing_WhenChatDoesNotReact()
    {
        // Arrange
        _battleChatService.GetReaction(CRIT_PARTS, Arg.Any<BattleContext>()).Returns((string)null);

        // Act
        await _battleService.HandleMessageAsync(CRIT_PARTS, ROOM_ID);

        // Assert
        _bot.DidNotReceiveWithAnyArgs().Say(null, null);
    }

    [Test]
    public async Task Test_GetBattlesInProgress_ShouldReturnOnlyBotsBattles_WhenNotOver()
    {
        // Arrange: the bot played ROOM_ID to the end, plays another battle and spectates a third
        const string playingRoom = "battle-gen9randombattle-2";
        const string spectatedRoom = "battle-gen9randombattle-3";
        SetUpBattleEnd("p1");
        SetUpRequest(playingRoom);
        await _battleService.HandleMessageAsync(WIN_PARTS, ROOM_ID);
        await _battleService.HandleMessageAsync(REQUEST_PARTS, playingRoom);
        await _battleService.HandleMessageAsync(START_PARTS, spectatedRoom);

        // Act
        var battles = _battleService.GetBattlesInProgress();

        // Assert
        Assert.That(battles.Select(battle => battle.RoomId), Is.EqualTo(new[] { playingRoom }));
    }
}
