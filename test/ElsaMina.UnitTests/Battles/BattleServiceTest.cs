using ElsaMina.Battles;
using ElsaMina.Battles.Replays;
using ElsaMina.Battles.Results;
using ElsaMina.Core;
using ElsaMina.Core.Services.Config;
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

    private IBattleMessageParser _messageParser;
    private IBot _bot;
    private ILadderingService _ladderingService;
    private IBattleResultsService _battleResultsService;
    private IReplaySavingService _replaySavingService;
    private BattleService _battleService;

    [SetUp]
    public void SetUp()
    {
        _messageParser = Substitute.For<IBattleMessageParser>();
        _bot = Substitute.For<IBot>();
        _ladderingService = Substitute.For<ILadderingService>();
        _battleResultsService = Substitute.For<IBattleResultsService>();
        _replaySavingService = Substitute.For<IReplaySavingService>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa-Mina");
        _battleService = new BattleService(_messageParser, Substitute.For<IBattleDecisionService>(), _bot,
            configuration, _ladderingService, _battleResultsService, _replaySavingService);
    }

    private void SetUpBattleEnd(string sideId, string winner = "Elsa-Mina")
    {
        _messageParser.TryApplyMessage(WIN_PARTS, ROOM_ID, Arg.Any<BattleContext>(), out Arg.Any<BattleMessageResult>())
            .Returns(callInfo =>
            {
                callInfo.ArgAt<BattleContext>(2).SideId = sideId;
                callInfo[3] = new BattleMessageResult(BattleMessageType.BattleEnded, winner);
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

    [TestCase("Elsa-Mina", BattleOutcome.Won)]
    [TestCase("Someone Else", BattleOutcome.Lost)]
    public async Task Test_HandleMessageAsync_ShouldRecordResult_WhenBotPlayedTheBattle(string winner,
        BattleOutcome expected)
    {
        // Arrange
        SetUpBattleEnd("p1", winner);

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
