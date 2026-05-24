using ElsaMina.Commands.Teams.Samples;
using ElsaMina.Core.Contexts;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Teams.Samples;

[TestFixture]
public class DeleteAllTeamsByTierCommandTest
{
    private DeleteAllTeamsByTierCommand _command;
    private IBotDbContextFactory _dbContextFactory;
    private IContext _context;
    private BotDbContext _dbContext;
    private DbContextOptions<BotDbContext> _dbContextOptions;

    private const string ROOM_ID = "testroom";
    private const string OTHER_ROOM_ID = "otherroom";

    [SetUp]
    public void SetUp()
    {
        _dbContextOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new BotDbContext(_dbContextOptions);

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<BotDbContext>(_dbContext));

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns(ROOM_ID);

        _command = new DeleteAllTeamsByTierCommand(_dbContextFactory);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelp_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Any<string>(), Arg.Any<bool>());
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNoTeamsFound_WhenNoTeamsMatchTierInRoom()
    {
        // Arrange
        _context.Target.Returns("gen9ou");
        var team = new Team { Id = "team1", Format = "gen9uu", Name = "Team 1" };
        _dbContext.Teams.Add(team);
        _dbContext.RoomTeams.Add(new RoomTeam { TeamId = "team1", RoomId = ROOM_ID });
        await _dbContext.SaveChangesAsync();

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("deleteallteams_no_teams_found", "gen9ou");
    }

    [Test]
    public async Task Test_RunAsync_ShouldDeleteRoomTeamsAndReplyWithSuccess_WhenTeamsMatchTierInRoom()
    {
        // Arrange
        _context.Target.Returns("gen9ou");
        _dbContext.Teams.Add(new Team { Id = "team1", Format = "gen9ou", Name = "Team 1" });
        _dbContext.Teams.Add(new Team { Id = "team2", Format = "gen9ou", Name = "Team 2" });
        _dbContext.RoomTeams.Add(new RoomTeam { TeamId = "team1", RoomId = ROOM_ID });
        _dbContext.RoomTeams.Add(new RoomTeam { TeamId = "team2", RoomId = ROOM_ID });
        await _dbContext.SaveChangesAsync();

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("deleteallteams_success", 2, "gen9ou");

        await using var verifyContext = new BotDbContext(_dbContextOptions);
        Assert.That(verifyContext.RoomTeams.Any(rt => rt.RoomId == ROOM_ID), Is.False);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDeleteOrphanedTeam_WhenTeamHasNoOtherRooms()
    {
        // Arrange
        _context.Target.Returns("gen9ou");
        _dbContext.Teams.Add(new Team { Id = "team1", Format = "gen9ou", Name = "Team 1" });
        _dbContext.RoomTeams.Add(new RoomTeam { TeamId = "team1", RoomId = ROOM_ID });
        await _dbContext.SaveChangesAsync();

        // Act
        await _command.RunAsync(_context);

        // Assert
        await using var verifyContext = new BotDbContext(_dbContextOptions);
        Assert.That(verifyContext.Teams.Any(t => t.Id == "team1"), Is.False);
    }

    [Test]
    public async Task Test_RunAsync_ShouldKeepTeam_WhenTeamBelongsToOtherRooms()
    {
        // Arrange
        _context.Target.Returns("gen9ou");
        _dbContext.Teams.Add(new Team { Id = "team1", Format = "gen9ou", Name = "Team 1" });
        _dbContext.RoomTeams.Add(new RoomTeam { TeamId = "team1", RoomId = ROOM_ID });
        _dbContext.RoomTeams.Add(new RoomTeam { TeamId = "team1", RoomId = OTHER_ROOM_ID });
        await _dbContext.SaveChangesAsync();

        // Act
        await _command.RunAsync(_context);

        // Assert
        await using var verifyContext = new BotDbContext(_dbContextOptions);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(verifyContext.Teams.Any(t => t.Id == "team1"), Is.True);
            Assert.That(verifyContext.RoomTeams.Any(rt => rt.TeamId == "team1" && rt.RoomId == OTHER_ROOM_ID), Is.True);
            Assert.That(verifyContext.RoomTeams.Any(rt => rt.TeamId == "team1" && rt.RoomId == ROOM_ID), Is.False);
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeTier_WhenInputHasSpacesOrMixedCase()
    {
        // Arrange
        _context.Target.Returns("Gen 9 OU");
        _dbContext.Teams.Add(new Team { Id = "team1", Format = "gen9ou", Name = "Team 1" });
        _dbContext.RoomTeams.Add(new RoomTeam { TeamId = "team1", RoomId = ROOM_ID });
        await _dbContext.SaveChangesAsync();

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("deleteallteams_success", 1, "Gen 9 OU");
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotDeleteTeamsFromOtherRooms_WhenSameTierExists()
    {
        // Arrange
        _context.Target.Returns("gen9ou");
        _dbContext.Teams.Add(new Team { Id = "team1", Format = "gen9ou", Name = "Team 1" });
        _dbContext.Teams.Add(new Team { Id = "team2", Format = "gen9ou", Name = "Team 2" });
        _dbContext.RoomTeams.Add(new RoomTeam { TeamId = "team1", RoomId = ROOM_ID });
        _dbContext.RoomTeams.Add(new RoomTeam { TeamId = "team2", RoomId = OTHER_ROOM_ID });
        await _dbContext.SaveChangesAsync();

        // Act
        await _command.RunAsync(_context);

        // Assert
        await using var verifyContext = new BotDbContext(_dbContextOptions);
        Assert.That(verifyContext.RoomTeams.Any(rt => rt.TeamId == "team2" && rt.RoomId == OTHER_ROOM_ID), Is.True);
    }
}
