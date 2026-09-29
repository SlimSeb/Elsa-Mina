using ElsaMina.Commands.Watchlist;
using ElsaMina.Core;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Watchlist;

public class StaffIntroChangeHandlerTest
{
    private IBot _bot;
    private StaffIntroChangeHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _bot = Substitute.For<IBot>();
        _handler = new StaffIntroChangeHandler(_bot);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldRequestStaffIntro_WhenStaffIntroChangeIsLogged()
    {
        string[] parts = ["", "c", "~", "/log Alice changed the staffintro."];

        await _handler.HandleReceivedMessageAsync(parts, "room");

        _bot.Received(1).Say("room", "/staffintro");
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnore_WhenMessageIsNotAStaffIntroLog()
    {
        string[] parts = ["", "c", "alice", "I changed the staffintro"];

        await _handler.HandleReceivedMessageAsync(parts, "room");

        _bot.DidNotReceiveWithAnyArgs().Say(default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnore_WhenMessageIsNotAChatMessage()
    {
        string[] parts = ["", "raw", "~", "/log Alice changed the staffintro."];

        await _handler.HandleReceivedMessageAsync(parts, "room");

        _bot.DidNotReceiveWithAnyArgs().Say(default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnore_WhenMessageHasTooFewParts()
    {
        string[] parts = ["", "c", "~"];

        await _handler.HandleReceivedMessageAsync(parts, "room");

        _bot.DidNotReceiveWithAnyArgs().Say(default, default);
    }
}
