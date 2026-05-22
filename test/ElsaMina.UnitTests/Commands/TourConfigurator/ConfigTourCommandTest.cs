using ElsaMina.Commands.TourConfigurator;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.TourConfigurator;

[TestFixture]
public class ConfigTourCommandTest
{
    private IRoomsManager _roomsManager;
    private ITourConfigService _tourConfigService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private ConfigTourCommand _sut;

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
        _context.Target.Returns(string.Empty);

        _sut = new ConfigTourCommand(_roomsManager, _tourConfigService, _templatesManager, _configuration);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_sut.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_sut.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseContextRoomId_WhenTargetIsEmpty()
    {
        _context.Target.Returns(string.Empty);
        _context.RoomId.Returns("myroom");
        _roomsManager.GetRoom("myroom").Returns(Substitute.For<IRoom>());
        _tourConfigService.GetTourConfigsForRoomAsync("myroom", Arg.Any<CancellationToken>())
            .Returns(new List<TourConfig>());
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("html");

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).GetTourConfigsForRoomAsync("myroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTargetAsRoomId_WhenTargetIsProvided()
    {
        _context.Target.Returns("targetroom");
        _roomsManager.GetRoom("targetroom").Returns(Substitute.For<IRoom>());
        _tourConfigService.GetTourConfigsForRoomAsync("targetroom", Arg.Any<CancellationToken>())
            .Returns(new List<TourConfig>());
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("html");

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).GetTourConfigsForRoomAsync("targetroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeTargetRoomId_WhenTargetHasUpperCaseAndSpaces()
    {
        _context.Target.Returns("  MyRoom  ");
        _roomsManager.GetRoom("myroom").Returns(Substitute.For<IRoom>());
        _tourConfigService.GetTourConfigsForRoomAsync("myroom", Arg.Any<CancellationToken>())
            .Returns(new List<TourConfig>());
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("html");

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).GetTourConfigsForRoomAsync("myroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomNotFound_WhenRoomDoesNotExist()
    {
        _context.Target.Returns("nonexistent");
        _roomsManager.GetRoom("nonexistent").Returns((IRoom)null);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tourconfig_room_not_found");
        await _tourConfigService.DidNotReceive().GetTourConfigsForRoomAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtmlPage_WhenRoomExists()
    {
        _context.Target.Returns(string.Empty);
        _context.RoomId.Returns("room1");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());
        _tourConfigService.GetTourConfigsForRoomAsync("room1", Arg.Any<CancellationToken>())
            .Returns(new List<TourConfig>());
        _templatesManager.GetTemplateAsync("TourConfigurator/TourConfigDashboard", Arg.Any<TourConfigDashboardViewModel>())
            .Returns("<html>dashboard</html>");

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyHtmlPage("room1tourconfig", Arg.Any<string>());
    }
}
