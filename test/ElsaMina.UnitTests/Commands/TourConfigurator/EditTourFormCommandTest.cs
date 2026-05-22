using ElsaMina.Commands.TourConfigurator;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.TourConfigurator;

[TestFixture]
public class EditTourFormCommandTest
{
    private IRoomsManager _roomsManager;
    private ITourConfigService _tourConfigService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private EditTourFormCommand _sut;

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

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("html");

        _sut = new EditTourFormCommand(_roomsManager, _tourConfigService, _templatesManager, _configuration);
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

        await _tourConfigService.DidNotReceive().GetTourConfigAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _context.DidNotReceive().ReplyHtmlPage(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenTargetIsWhitespace()
    {
        _context.Target.Returns("  ");

        await _sut.RunAsync(_context);

        await _tourConfigService.DidNotReceive().GetTourConfigAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomNotFound_WhenRoomDoesNotExist()
    {
        _context.Target.Returns("outils,room1");
        _roomsManager.GetRoom("room1").Returns((IRoom)null);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tourconfig_room_not_found");
        await _tourConfigService.DidNotReceive().GetTourConfigAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseContextRoomId_WhenNoRoomArgProvided()
    {
        _context.Target.Returns("outils");
        _context.RoomId.Returns("contextroom");
        _roomsManager.GetRoom("contextroom").Returns(Substitute.For<IRoom>());
        _tourConfigService.GetTourConfigAsync("outils", "contextroom", Arg.Any<CancellationToken>())
            .Returns(new TourConfig { Id = "outils", RoomId = "contextroom", Tier = "OU", Format = "elim", Autostart = 5 });

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).GetTourConfigAsync("outils", "contextroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenInsufficientRank()
    {
        _context.Target.Returns("outils,room1");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());
        _context.HasSufficientRankInRoom("room1", Rank.Driver, Arg.Any<CancellationToken>()).Returns(false);

        await _sut.RunAsync(_context);

        await _tourConfigService.DidNotReceive().GetTourConfigAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyConfigNotFound_WhenConfigDoesNotExist()
    {
        _context.Target.Returns("outils,room1");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());
        _tourConfigService.GetTourConfigAsync("outils", "room1", Arg.Any<CancellationToken>())
            .Returns((TourConfig)null);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tourconfig_not_found", "outils");
        _context.DidNotReceive().ReplyHtmlPage(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtmlPage_WhenConfigExists()
    {
        _context.Target.Returns("outils,room1");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());
        _tourConfigService.GetTourConfigAsync("outils", "room1", Arg.Any<CancellationToken>())
            .Returns(new TourConfig { Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 5 });
        _templatesManager.GetTemplateAsync("TourConfigurator/TourConfigEditForm", Arg.Any<TourConfigFormViewModel>())
            .Returns("<html>form</html>");

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyHtmlPage("room1tourconfig", Arg.Any<string>());
    }
}
