using ElsaMina.Commands.Teams;
using ElsaMina.Commands.Teams.Samples;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Teams.Samples;

[TestFixture]
public class TeamListCommandTest
{
    private IContext _context;
    private ITemplatesManager _templatesManager;
    private IBotDbContextFactory _dbContextFactory;
    private DbContextOptions<BotDbContext> _dbOptions;
    private TeamListCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));
        _command = new TeamListCommand(_templatesManager, _dbContextFactory);
    }

    [TearDown]
    public async Task TearDown()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.Database.EnsureDeletedAsync();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyTeamListEmpty_WhenNoTeamsExistInRoom()
    {
        _context.Target.Returns(string.Empty);
        _context.RoomId.Returns("testroom");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("team_list_empty");
        await _templatesManager.DidNotReceive()
            .GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyTeamListEmpty_WhenNoTeamsMatchFormat()
    {
        await using (var seedContext = new BotDbContext(_dbOptions))
        {
            var team = new Team
            {
                Id = "team1",
                Name = "My Team",
                Format = "gen9ou",
                Rooms = [new RoomTeam { RoomId = "testroom" }]
            };
            await seedContext.Teams.AddAsync(team);
            await seedContext.SaveChangesAsync();
        }

        _context.Target.Returns("gen8ou, testroom");
        _context.RoomId.Returns("testroom");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("team_list_empty");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallTemplate_WhenTeamsExistInRoom()
    {
        await using (var seedContext = new BotDbContext(_dbOptions))
        {
            var team = new Team
            {
                Id = "team1",
                Name = "My Team",
                Format = "gen9ou",
                Rooms = [new RoomTeam { RoomId = "testroom" }]
            };
            await seedContext.Teams.AddAsync(team);
            await seedContext.SaveChangesAsync();
        }

        _context.Target.Returns(string.Empty);
        _context.RoomId.Returns("testroom");
        _context.HasRankOrHigher(Rank.Voiced).Returns(true);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<TeamListViewModel>())
            .Returns("<html/>");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1)
            .GetTemplateAsync("Teams/TeamList", Arg.Any<TeamListViewModel>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldFilterByFormat_WhenFormatIsProvided()
    {
        await using (var seedContext = new BotDbContext(_dbOptions))
        {
            var ouTeam = new Team { Id = "t1", Name = "OU Team", Format = "gen9ou", Rooms = [new RoomTeam { RoomId = "testroom" }] };
            var ubersTeam = new Team { Id = "t2", Name = "Ubers Team", Format = "gen9ubers", Rooms = [new RoomTeam { RoomId = "testroom" }] };
            await seedContext.Teams.AddRangeAsync(ouTeam, ubersTeam);
            await seedContext.SaveChangesAsync();
        }

        _context.Target.Returns("gen9ou");
        _context.RoomId.Returns("testroom");
        _context.HasRankOrHigher(Rank.Voiced).Returns(true);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<TeamListViewModel>())
            .Returns("<html/>");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1)
            .GetTemplateAsync("Teams/TeamList",
                Arg.Is<TeamListViewModel>(vm => vm.Teams.Count() == 1 && vm.Teams.First().Format == "gen9ou"));
    }

    [TestCase("gen9ou", "ou")]
    [TestCase("ou", "gen9ou")]
    [TestCase("gen9nationaldex", "natdex")]
    public async Task Test_RunAsync_ShouldListTeamsSavedUnderAnotherNameOfFormat_WhenFormatIsProvided(string format,
        string savedFormat)
    {
        await using (var seedContext = new BotDbContext(_dbOptions))
        {
            var team = new Team { Id = "t1", Name = "Team", Format = savedFormat, Rooms = [new RoomTeam { RoomId = "testroom" }] };
            var otherGenerationTeam = new Team { Id = "t2", Name = "Gen 8 Team", Format = "gen8ou", Rooms = [new RoomTeam { RoomId = "testroom" }] };
            await seedContext.Teams.AddRangeAsync(team, otherGenerationTeam);
            await seedContext.SaveChangesAsync();
        }

        _context.Target.Returns(format);
        _context.RoomId.Returns("testroom");
        _context.HasRankOrHigher(Rank.Voiced).Returns(true);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<TeamListViewModel>())
            .Returns("<html/>");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1)
            .GetTemplateAsync("Teams/TeamList",
                Arg.Is<TeamListViewModel>(vm => vm.Teams.Count() == 1 && vm.Teams.First().Id == "t1"));
    }
}
