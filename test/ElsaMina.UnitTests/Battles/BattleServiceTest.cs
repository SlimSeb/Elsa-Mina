using ElsaMina.Battles;
using ElsaMina.Core;
using ElsaMina.Core.Services.Config;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles;

[TestFixture]
public class BattleServiceTest
{
    private const string ROOM_ID = "battle-gen9randombattle-1";
    private static readonly string[] WIN_PARTS = ["", "win", "Elsa-Mina"];

    private IBattleMessageParser _messageParser;
    private ILadderingService _ladderingService;
    private BattleService _battleService;

    [SetUp]
    public void SetUp()
    {
        _messageParser = Substitute.For<IBattleMessageParser>();
        _ladderingService = Substitute.For<ILadderingService>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa-Mina");
        _battleService = new BattleService(_messageParser, Substitute.For<IBattleDecisionService>(),
            Substitute.For<IBot>(), configuration, _ladderingService);
    }

    private void SetUpBattleEnd(string sideId)
    {
        _messageParser.TryApplyMessage(WIN_PARTS, ROOM_ID, Arg.Any<BattleContext>(), out Arg.Any<BattleMessageResult>())
            .Returns(callInfo =>
            {
                callInfo.ArgAt<BattleContext>(2).SideId = sideId;
                callInfo[3] = new BattleMessageResult(BattleMessageType.BattleEnded, "Elsa-Mina");
                return true;
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
}
