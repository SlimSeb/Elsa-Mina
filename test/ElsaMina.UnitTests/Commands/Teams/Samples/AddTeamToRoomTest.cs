using ElsaMina.Commands.Teams.Samples;
using ElsaMina.Core.Contexts;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Teams.Samples;

public class AddTeamToRoomCommandTests
{
    private AddTeamToRoomCommand _command;
    private IBotDbContextFactory _dbContextFactory;
    private BotDbContext _dbContext;
    private IContext _context;
    private DbContextOptions<BotDbContext> _options;

    [SetUp]
    public void SetUp()
    {
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _context = Substitute.For<IContext>();

        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new BotDbContext(_options);

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_dbContext));

        _command = new AddTeamToRoomCommand(_dbContextFactory);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNoArgMessage_WhenTeamIdIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("add_team_to_room_no_arg");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNoTeamMessage_WhenTeamNotFound()
    {
        // Arrange
        _context.Target.Returns("teamId");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("add_team_to_room_no_team");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithAlreadyInRoomMessage_WhenTeamAlreadyInRoom()
    {
        // Arrange
        _context.Target.Returns("teamId");
        _context.RoomId.Returns("roomId");

        var team = new Team
        {
            Id = "teamid",
            Rooms = new List<RoomTeam>
            {
                new RoomTeam { RoomId = "roomId", TeamId = "teamid" }
            }
        };

        _dbContext.Teams.Add(team);
        await _dbContext.SaveChangesAsync();

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("add_team_to_room_team_already_in_room");
    }

    [Test]
    public async Task Test_RunAsync_ShouldAddRoomAndReplyWithSuccess_WhenTeamIsValidAndNotInRoom()
    {
        // Arrange
        _context.Target.Returns("teamId");
        _context.RoomId.Returns("roomId");

        var team = new Team
        {
            Id = "teamid",
            Rooms = new List<RoomTeam>()
        };

        _dbContext.Teams.Add(team);
        await _dbContext.SaveChangesAsync();

        // Act
        await _command.RunAsync(_context);

        // Assert
        await using var dbContext = new BotDbContext(_options);
        var updated = await dbContext.Teams.Include(otherTeam => otherTeam.Rooms).SingleAsync();
        Assert.That(updated.Rooms.Any(roomTeam =>
            roomTeam.RoomId == "roomId" && roomTeam.TeamId == "teamid"), Is.True);
        _context.Received().ReplyLocalizedMessage("add_team_to_room_success");
    }
    
    [Test]
    public async Task Test_RunAsync_ShouldReplyWithFailureMessage_WhenRepositoryUpdateThrowsException()
    {
        // Arrange
        _context.Target.Returns("teamId");
        _context.RoomId.Returns("roomId");

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Throws(new Exception("db error"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("add_team_to_room_failure", "db error");
    }
}
