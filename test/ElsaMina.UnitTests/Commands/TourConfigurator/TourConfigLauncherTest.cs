using ElsaMina.Commands.TourConfigurator;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess.Models;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.TourConfigurator;

[TestFixture]
public class TourConfigLauncherTest
{
    private ITourConfigService _tourConfigService;
    private IContext _context;
    private TourConfigLauncher _sut;

    [SetUp]
    public void SetUp()
    {
        _tourConfigService = Substitute.For<ITourConfigService>();
        _context = Substitute.For<IContext>();
        _sut = new TourConfigLauncher(_tourConfigService);
    }

    [Test]
    public async Task Test_TryExecuteAsync_ShouldReturnFalse_WhenIsPrivateMessage()
    {
        _context.IsPrivateMessage.Returns(true);

        var result = await _sut.TryExecuteAsync("tourname", _context);

        Assert.That(result, Is.False);
        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_TryExecuteAsync_ShouldReturnFalse_WhenUserHasInsufficientRank()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.HasRankOrHigher(Rank.Driver).Returns(false);

        var result = await _sut.TryExecuteAsync("tourname", _context);

        Assert.That(result, Is.False);
        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_TryExecuteAsync_ShouldReturnFalse_WhenConfigNotFound()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.HasRankOrHigher(Rank.Driver).Returns(true);
        _context.RoomId.Returns("room1");
        _tourConfigService.GetTourConfigAsync("unknown", "room1").Returns((TourConfig)null);

        var result = await _sut.TryExecuteAsync("unknown", _context);

        Assert.That(result, Is.False);
        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_TryExecuteAsync_ShouldReturnTrue_WhenConfigFound()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.HasRankOrHigher(Rank.Driver).Returns(true);
        _context.RoomId.Returns("room1");
        _tourConfigService.GetTourConfigAsync("outils", "room1").Returns(new TourConfig
        {
            Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 0
        });

        var result = await _sut.TryExecuteAsync("outils", _context);

        Assert.That(result, Is.True);
    }

    [Test]
    public void Test_LaunchTournament_ShouldSendCreateCommand()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("room1");
        var config = new TourConfig { Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 0 };

        TourConfigLauncher.LaunchTournament(_context, config);

        _context.Received(1).SendMessageIn("room1", "/tour create OU, elim");
    }

    [Test]
    public void Test_LaunchTournament_ShouldSendAutostartCommand_WhenAutostartIsPositive()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("room1");
        var config = new TourConfig { Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 10 };

        TourConfigLauncher.LaunchTournament(_context, config);

        _context.Received(1).SendMessageIn("room1", "/tour autostart 10");
    }

    [Test]
    public void Test_LaunchTournament_ShouldNotSendAutostartCommand_WhenAutostartIsZero()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("room1");
        var config = new TourConfig { Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 0 };

        TourConfigLauncher.LaunchTournament(_context, config);

        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Is<string>(s => s.StartsWith("/tour autostart")));
    }

    [Test]
    public void Test_LaunchTournament_ShouldSendAutoDqCommand_WhenAutoDqHasValue()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("room1");
        var config = new TourConfig
        {
            Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 0, AutoDq = 5
        };

        TourConfigLauncher.LaunchTournament(_context, config);

        _context.Received(1).SendMessageIn("room1", "/tour autodq 5");
    }

    [Test]
    public void Test_LaunchTournament_ShouldNotSendAutoDqCommand_WhenAutoDqIsNull()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("room1");
        var config = new TourConfig
        {
            Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 0, AutoDq = null
        };

        TourConfigLauncher.LaunchTournament(_context, config);

        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Is<string>(s => s.StartsWith("/tour autodq")));
    }

    [Test]
    public void Test_LaunchTournament_ShouldSendNameCommand_WhenTourNameIsSet()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("room1");
        var config = new TourConfig
        {
            Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 0, TourName = "My Tour"
        };

        TourConfigLauncher.LaunchTournament(_context, config);

        _context.Received(1).SendMessageIn("room1", "/tour name My Tour");
    }

    [Test]
    public void Test_LaunchTournament_ShouldNotSendNameCommand_WhenTourNameIsEmpty()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("room1");
        var config = new TourConfig
        {
            Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 0, TourName = ""
        };

        TourConfigLauncher.LaunchTournament(_context, config);

        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Is<string>(s => s.StartsWith("/tour name")));
    }

    [Test]
    public void Test_LaunchTournament_ShouldSendRulesCommand_WhenRulesAreSet()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("room1");
        var config = new TourConfig
        {
            Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 0, Rules = "Sleep Clause"
        };

        TourConfigLauncher.LaunchTournament(_context, config);

        _context.Received(1).SendMessageIn("room1", "/tour rules Sleep Clause");
    }

    [Test]
    public void Test_LaunchTournament_ShouldNotSendRulesCommand_WhenRulesAreEmpty()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("room1");
        var config = new TourConfig
        {
            Id = "outils", RoomId = "room1", Tier = "OU", Format = "elim", Autostart = 0, Rules = ""
        };

        TourConfigLauncher.LaunchTournament(_context, config);

        _context.DidNotReceive().SendMessageIn(Arg.Any<string>(), Arg.Is<string>(s => s.StartsWith("/tour rules")));
    }

    [Test]
    public void Test_LaunchTournament_ShouldUseConfigRoomId_WhenContextIsPrivateMessage()
    {
        _context.IsPrivateMessage.Returns(true);
        var config = new TourConfig
        {
            Id = "outils", RoomId = "configroom", Tier = "OU", Format = "elim", Autostart = 0
        };

        TourConfigLauncher.LaunchTournament(_context, config);

        _context.Received(1).SendMessageIn("configroom", "/tour create OU, elim");
    }
}
