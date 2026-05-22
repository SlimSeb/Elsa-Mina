using ElsaMina.Commands.TourConfigurator;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.TourConfigurator;

[TestFixture]
public class DeleteTourCommandTest
{
    private IRoomsManager _roomsManager;
    private ITourConfigService _tourConfigService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private DeleteTourCommand _sut;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _tourConfigService = Substitute.For<ITourConfigService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();

        _configuration.Name.Returns("BotName");
        _configuration.Trigger.Returns("-");
        _context.RoomId.Returns("room1");
        _context.HasSufficientRankInRoom(Arg.Any<string>(), Arg.Any<Rank>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _tourConfigService.GetTourConfigsForRoomAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<TourConfig>());
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("html");

        _sut = new DeleteTourCommand(_roomsManager, _tourConfigService, _templatesManager, _configuration);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_sut.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_IsPrivateMessageOnly_ShouldBeTrue()
    {
        Assert.That(_sut.IsPrivateMessageOnly, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenTargetIsEmpty()
    {
        _context.Target.Returns(string.Empty);

        await _sut.RunAsync(_context);

        await _tourConfigService.DidNotReceive().DeleteTourConfigAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenTargetIsWhitespace()
    {
        _context.Target.Returns("  ");

        await _sut.RunAsync(_context);

        await _tourConfigService.DidNotReceive().DeleteTourConfigAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomNotFound_WhenRoomDoesNotExist()
    {
        _context.Target.Returns("tour1,room1");
        _roomsManager.GetRoom("room1").Returns((IRoom)null);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tourconfig_room_not_found");
        await _tourConfigService.DidNotReceive().DeleteTourConfigAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseContextRoomId_WhenNoRoomArgProvided()
    {
        _context.Target.Returns("tour1");
        _context.RoomId.Returns("contextroom");
        _roomsManager.GetRoom("contextroom").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).DeleteTourConfigAsync("tour1", "contextroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseProvidedRoomId_WhenRoomArgGiven()
    {
        _context.Target.Returns("tour1,targetroom");
        _roomsManager.GetRoom("targetroom").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).DeleteTourConfigAsync("tour1", "targetroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenInsufficientRank()
    {
        _context.Target.Returns("tour1,room1");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());
        _context.HasSufficientRankInRoom("room1", Rank.Driver, Arg.Any<CancellationToken>()).Returns(false);

        await _sut.RunAsync(_context);

        await _tourConfigService.DidNotReceive().DeleteTourConfigAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDeleteAndReply_WhenValidArgs()
    {
        _context.Target.Returns("tour1,room1");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).DeleteTourConfigAsync("tour1", "room1", Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("tourconfig_deleted", "tour1");
    }

    [Test]
    public async Task Test_RunAsync_ShouldRefreshDashboard_AfterDeleting()
    {
        _context.Target.Returns("tour1,room1");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyHtmlPage("room1tourconfig", Arg.Any<string>());
    }
}
