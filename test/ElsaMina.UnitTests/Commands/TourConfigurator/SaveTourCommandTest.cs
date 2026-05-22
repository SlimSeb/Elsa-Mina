using ElsaMina.Commands.TourConfigurator;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.TourConfigurator;

[TestFixture]
public class SaveTourCommandTest
{
    private IRoomsManager _roomsManager;
    private ITourConfigService _tourConfigService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private SaveTourCommand _sut;

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

        _sut = new SaveTourCommand(_roomsManager, _tourConfigService, _templatesManager, _configuration);
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
    public async Task Test_RunAsync_ShouldReplyMissingData_WhenTargetIsEmpty()
    {
        _context.Target.Returns(string.Empty);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tourconfig_missing_data");
        await _tourConfigService.DidNotReceive().SaveTourConfigAsync(Arg.Any<TourConfig>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyMissingData_WhenTargetIsWhitespace()
    {
        _context.Target.Returns("   ");

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tourconfig_missing_data");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidFormat_WhenFewerThan8Parts()
    {
        _context.Target.Returns("room1;;tourid;;OU;;elim;;10;;5;;tourname");

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tourconfig_invalid_format");
        await _tourConfigService.DidNotReceive().SaveTourConfigAsync(Arg.Any<TourConfig>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyIdRequired_WhenTourIdIsEmpty()
    {
        _context.Target.Returns("room1;;  ;;OU;;elim;;10;;5;;tourname;;rule");

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tourconfig_id_required");
        await _tourConfigService.DidNotReceive().SaveTourConfigAsync(Arg.Any<TourConfig>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyTierRequired_WhenTierIsEmpty()
    {
        _context.Target.Returns("room1;;tourid;;  ;;elim;;10;;5;;tourname;;rule");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tourconfig_tier_required");
        await _tourConfigService.DidNotReceive().SaveTourConfigAsync(Arg.Any<TourConfig>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomNotFound_WhenRoomDoesNotExist()
    {
        _context.Target.Returns("room1;;tourid;;OU;;elim;;10;;5;;tourname;;rule");
        _roomsManager.GetRoom("room1").Returns((IRoom)null);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tourconfig_room_not_found");
        await _tourConfigService.DidNotReceive().SaveTourConfigAsync(Arg.Any<TourConfig>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenInsufficientRank()
    {
        _context.Target.Returns("room1;;tourid;;OU;;elim;;10;;5;;tourname;;rule");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());
        _context.HasSufficientRankInRoom("room1", Rank.Driver, Arg.Any<CancellationToken>()).Returns(false);

        await _sut.RunAsync(_context);

        await _tourConfigService.DidNotReceive().SaveTourConfigAsync(Arg.Any<TourConfig>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSaveTourConfig_WhenAllDataValid()
    {
        _context.Target.Returns("room1;;outils;;OU;;elim;;10;;5;;OU Tour;;Sleep Clause");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).SaveTourConfigAsync(
            Arg.Is<TourConfig>(c =>
                c.Id == "outils" &&
                c.RoomId == "room1" &&
                c.Tier == "OU" &&
                c.Format == "elim" &&
                c.Autostart == 10 &&
                c.AutoDq == 5 &&
                c.TourName == "OU Tour" &&
                c.Rules == "Sleep Clause"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplySaved_WhenSaveSucceeds()
    {
        _context.Target.Returns("room1;;outils;;OU;;elim;;10;;5;;OU Tour;;Sleep Clause");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tourconfig_saved", "outils");
    }

    [Test]
    public async Task Test_RunAsync_ShouldDefaultAutostartTo10_WhenAutostartIsNotANumber()
    {
        _context.Target.Returns("room1;;outils;;OU;;elim;;notanumber;;5;;tourname;;rules");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).SaveTourConfigAsync(
            Arg.Is<TourConfig>(c => c.Autostart == 10),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetAutoDqToNull_WhenAutoDqIsNotANumber()
    {
        _context.Target.Returns("room1;;outils;;OU;;elim;;10;;notanumber;;tourname;;rules");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).SaveTourConfigAsync(
            Arg.Is<TourConfig>(c => c.AutoDq == null),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDefaultFormatToElim_WhenFormatIsWhitespace()
    {
        _context.Target.Returns("room1;;outils;;OU;;   ;;10;;5;;tourname;;rules");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).SaveTourConfigAsync(
            Arg.Is<TourConfig>(c => c.Format == "elim"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeTourId_WhenTourIdHasSpacesAndUpperCase()
    {
        _context.Target.Returns("room1;; My Tour ;;OU;;elim;;10;;5;;tourname;;rules");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).SaveTourConfigAsync(
            Arg.Is<TourConfig>(c => c.Id == "mytour"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldJoinRulesWithDoubleSemicolon_WhenRulesContainSemicolonSeparators()
    {
        _context.Target.Returns("room1;;outils;;OU;;elim;;10;;5;;tourname;;rule1;;rule2;;rule3");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        await _tourConfigService.Received(1).SaveTourConfigAsync(
            Arg.Is<TourConfig>(c => c.Rules == "rule1;;rule2;;rule3"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRefreshDashboard_AfterSaving()
    {
        _context.Target.Returns("room1;;outils;;OU;;elim;;10;;5;;tourname;;rules");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyHtmlPage("room1tourconfig", Arg.Any<string>());
    }
}
