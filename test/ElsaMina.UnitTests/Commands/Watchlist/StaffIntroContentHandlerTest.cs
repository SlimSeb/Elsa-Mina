using ElsaMina.Commands.Watchlist;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Watchlist;

public class StaffIntroContentHandlerTest
{
    private IWatchlistService _watchlistService;
    private StaffIntroContentHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _watchlistService = Substitute.For<IWatchlistService>();
        _handler = new StaffIntroContentHandler(_watchlistService);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldForwardRejoinedHtml_WhenRawMessageIsReceived()
    {
        string[] parts = ["", "raw", "<div>a", "b</div>"];

        await _handler.HandleReceivedMessageAsync(parts, "room");

        _watchlistService.Received(1).HandleReceivedStaffIntro("room", "<div>a|b</div>");
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnore_WhenMessageIsNotRaw()
    {
        string[] parts = ["", "c", "<div></div>"];

        await _handler.HandleReceivedMessageAsync(parts, "room");

        _watchlistService.DidNotReceiveWithAnyArgs().HandleReceivedStaffIntro(default, default);
    }

    [Test]
    public async Task Test_HandleReceivedMessageAsync_ShouldIgnore_WhenMessageHasTooFewParts()
    {
        string[] parts = ["", "raw"];

        await _handler.HandleReceivedMessageAsync(parts, "room");

        _watchlistService.DidNotReceiveWithAnyArgs().HandleReceivedStaffIntro(default, default);
    }
}
