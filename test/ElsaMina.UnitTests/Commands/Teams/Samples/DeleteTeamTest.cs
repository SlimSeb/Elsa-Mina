using ElsaMina.Commands.Teams.Samples;
using ElsaMina.Core.Contexts;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Teams.Samples;

public class DeleteTeamCommandTests
{
    private DeleteTeamCommand _command;
    private IBotDbContextFactory _dbContextFactory;
    private IContext _context;
    private BotDbContext _dbContext;
    private DbContextOptions<BotDbContext> _dbContextOptions;

    [SetUp]
    public void SetUp()
    {
        _dbContextOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new BotDbContext(_dbContextOptions);

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<BotDbContext>(_dbContext));

        _command = new DeleteTeamCommand(_dbContextFactory);

        _context = Substitute.For<IContext>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithTeamNotFound_WhenTeamDoesNotExist()
    {
        // Arrange
        _context.Target.Returns("nonexistentTeamId");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("deleteteam_team_not_found");
    }

    [Test]
    public async Task Test_RunAsync_ShouldDeleteTeamAndReplyWithSuccess_WhenTeamExists()
    {
        // Arrange
        var team = new Team { Id = "teamid" };
        _dbContext.Teams.Add(team);
        await _dbContext.SaveChangesAsync();

        _context.Target.Returns("teamId");

        // Act
        await _command.RunAsync(_context);
        
        // Assert
        await using var dbContext = new BotDbContext(_dbContextOptions);
        Assert.That(await dbContext.Teams.AnyAsync(t => t.Id == "teamid"), Is.False);
        _context.Received().ReplyLocalizedMessage("deleteteam_team_deleted_successfully");
    }
}
