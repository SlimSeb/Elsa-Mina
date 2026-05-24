using ElsaMina.Commands.Teams;
using ElsaMina.Commands.Teams.Samples;
using ElsaMina.Commands.Teams.TeamProviders;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Teams.Samples;

public class AddTeamCommandTests
{
    private AddTeamCommand _command;
    private ITeamLinkMatchFactory _teamLinkMatchFactory;
    private IClockService _clockService;
    private IBotDbContextFactory _dbContextFactory;
    private BotDbContext _dbContext;
    private IContext _context;
    private DbContextOptions<BotDbContext> _options;

    [SetUp]
    public void SetUp()
    {
        _teamLinkMatchFactory = Substitute.For<ITeamLinkMatchFactory>();
        _clockService = Substitute.For<IClockService>();
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _context = Substitute.For<IContext>();

        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new BotDbContext(_options);

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_dbContext));

        _command = new AddTeamCommand(_teamLinkMatchFactory, _clockService, _dbContextFactory);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenTargetIsInvalid()
    {
        // Arrange
        _context.Target.Returns("invalid_target");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received().Reply(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNameTooLongMessage_WhenNameExceedsMaxLength()
    {
        // Arrange
        _context.Target.Returns("link, " + new string('a', 71) + ", format");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("add_team_name_too_long");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNoProviderMessage_WhenTeamLinkMatchNotFound()
    {
        // Arrange
        _context.Target.Returns("link, name, format");
        _teamLinkMatchFactory.FindTeamLinkMatch("link").Returns((ITeamLinkMatch)null);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("add_team_no_provider");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNoExportErrorMessage_WhenTeamExportIsNull()
    {
        // Arrange
        _context.Target.Returns("link, name, format");

        var teamLinkMatch = Substitute.For<ITeamLinkMatch>();
        teamLinkMatch.GetTeamExport().Returns(Task.FromResult((SharedTeam)null));

        _teamLinkMatchFactory.FindTeamLinkMatch("link").Returns(teamLinkMatch);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("add_team_no_export_error");
    }

    [Test]
    public async Task Test_RunAsync_ShouldAddTeamToRepository_WhenDataIsValid()
    {
        // Arrange
        _context.Target.Returns("link, name, format");
        _context.RoomId.Returns("room");
        _context.Sender.Name.Returns("author");

        var teamLinkMatch = Substitute.For<ITeamLinkMatch>();
        var sharedTeam = new SharedTeam { TeamExport = "export_data" };
        teamLinkMatch.GetTeamExport().Returns(Task.FromResult(sharedTeam));

        _teamLinkMatchFactory.FindTeamLinkMatch("link").Returns(teamLinkMatch);

        var currentDateTime = DateTime.UtcNow;
        _clockService.CurrentUtcDateTime.Returns(currentDateTime);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await using var dbContext = new BotDbContext(_options);
        var team = dbContext.Teams.Include(team => team.Rooms).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.Id, Is.EqualTo("name".ToLowerAlphaNum()));
            Assert.That(team.Name, Is.EqualTo("name"));
            Assert.That(team.Author, Is.EqualTo("author"));
            Assert.That(team.Link, Is.EqualTo("link"));
            Assert.That(team.CreationDate, Is.EqualTo(currentDateTime));
            Assert.That(team.TeamJson, Is.EqualTo(ShowdownTeamsUtils.TeamExportToJson("export_data")));
            Assert.That(team.Format, Is.EqualTo("format"));
            Assert.That(team.Rooms.Count, Is.EqualTo(1));
            Assert.That(team.Rooms.ElementAt(0).RoomId, Is.EqualTo("room"));
            Assert.That(team.Rooms.ElementAt(0).TeamId, Is.EqualTo("name".ToLowerAlphaNum()));
        }

        _context.Received().ReplyLocalizedMessage("add_team_success", "name".ToLowerAlphaNum());
    }

    [Test]
    public async Task Test_RunAsync_ShouldAddTeamToBothRooms_WhenRoomIdIsFrenchOrArcade()
    {
        // Arrange
        _context.Target.Returns("link, name, format");
        _context.RoomId.Returns("arcade");
        _context.Sender.Name.Returns("author");

        var teamLinkMatch = Substitute.For<ITeamLinkMatch>();
        var sharedTeam = new SharedTeam { TeamExport = "export_data" };
        teamLinkMatch.GetTeamExport().Returns(Task.FromResult(sharedTeam));

        _teamLinkMatchFactory.FindTeamLinkMatch("link").Returns(teamLinkMatch);

        var currentDateTime = DateTime.UtcNow;
        _clockService.CurrentUtcDateTime.Returns(currentDateTime);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await using var dbContext = new BotDbContext(_options);
        var team = dbContext.Teams.Include(team => team.Rooms).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.Rooms.Count, Is.EqualTo(2));
            Assert.That(team.Rooms.Any(rt => rt.RoomId == "arcade"));
            Assert.That(team.Rooms.Any(rt => rt.RoomId == "franais"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithFailureMessage_WhenRepositoryThrowsException()
    {
        // Arrange
        _context.Target.Returns("link, name, format");
        _context.RoomId.Returns("room");
        _context.Sender.Name.Returns("author");

        var teamLinkMatch = Substitute.For<ITeamLinkMatch>();
        var sharedTeam = new SharedTeam { TeamExport = "export_data" };
        teamLinkMatch.GetTeamExport().Returns(Task.FromResult(sharedTeam));

        _teamLinkMatchFactory.FindTeamLinkMatch("link").Returns(teamLinkMatch);

        var faultyOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var throwingDbContext = Substitute.For<BotDbContext>(faultyOptions);
        throwingDbContext.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<int>(new Exception("Database error")));

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((BotDbContext)throwingDbContext));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("add_team_failure", Arg.Any<string>());
    }
}
