using ElsaMina.Commands.Badges.HallOfFame;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Badges.HallOfFame;

public class HallOfFameCommandTest
{
    private IContext _context;
    private ITemplatesManager _templatesManager;
    private IBotDbContextFactory _dbContextFactory;
    private HallOfFameCommand _command;

    private static DbContextOptions<BotDbContext> CreateNewInMemoryOptions()
    {
        return new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _command = new HallOfFameCommand(_templatesManager, _dbContextFactory);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoTrophies_WhenNoTrophiesFound()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);
        _dbContextFactory.CreateDbContextAsync(default).Returns(dbContext);

        _context.RoomId.Returns("room1");
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("hall_of_fame_no_trophies", "room1");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldGenerateTemplate_WhenTrophiesExist()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);

        var trophy1 = new Badge
        {
            Id = "trophy1",
            Name = "Trophy 1",
            RoomId = "room1",
            IsTrophy = true,
            IsTeamTournament = false
        };
        var trophy2 = new Badge
        {
            Id = "trophy2",
            Name = "Trophy 2",
            RoomId = "room1",
            IsTrophy = true,
            IsTeamTournament = true
        };

        var user1 = "user1";
        var user2 = "user2";

        trophy1.BadgeHolders.Add(new BadgeHolding { UserId = user1, BadgeId = trophy1.Id, RoomId = trophy1.RoomId });
        trophy2.BadgeHolders.Add(new BadgeHolding { UserId = user1, BadgeId = trophy2.Id, RoomId = trophy2.RoomId });
        trophy2.BadgeHolders.Add(new BadgeHolding { UserId = user2, BadgeId = trophy2.Id, RoomId = trophy2.RoomId });

        await dbContext.RoomUsers.AddAsync(new RoomUser
            { Id = user1, RoomId = "room1", User = new SavedUser { UserId = user1, UserName = "User 1" } });
        await dbContext.Badges.AddRangeAsync(trophy1, trophy2);
        await dbContext.SaveChangesAsync();

        _dbContextFactory.CreateDbContextAsync(default).Returns(dbContext);

        _context.RoomId.Returns("room1");
        _context.Target.Returns(string.Empty);
        _context.Culture.Returns(System.Globalization.CultureInfo.InvariantCulture);

        var expectedHtml = "<div>Hall of Fame</div>";
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns(expectedHtml);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Badges/HallOfFame/HallOfFame",
            Arg.Is<HallOfFameViewModel>(vm =>
                vm.SortedPlayerRecords.Length == 2 &&
                vm.SortedPlayerRecords[0].UserId == user1 &&
                vm.SortedPlayerRecords[0].UserName == "User 1" &&
                vm.SortedPlayerRecords[0].Total == 2 &&
                vm.SortedPlayerRecords[0].Solo == 1 &&
                vm.SortedPlayerRecords[0].Team == 1 &&
                vm.SortedPlayerRecords[1].UserId == user2 &&
                vm.SortedPlayerRecords[1].UserName == user2 &&
                vm.SortedPlayerRecords[1].Total == 1 &&
                vm.SortedPlayerRecords[1].Solo == 0 &&
                vm.SortedPlayerRecords[1].Team == 1
            ));

        _context.Received(1).ReplyHtml(expectedHtml, null, true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTargetRoom_WhenProvided()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using var dbContext = new BotDbContext(options);

        var trophy = new Badge
        {
            Id = "trophy1",
            Name = "Trophy 1",
            RoomId = "targetroom",
            IsTrophy = true
        };
        trophy.BadgeHolders.Add(new BadgeHolding { UserId = "user1", BadgeId = trophy.Id, RoomId = trophy.RoomId });

        await dbContext.Badges.AddAsync(trophy);
        await dbContext.SaveChangesAsync();

        _dbContextFactory.CreateDbContextAsync(default).Returns(dbContext);

        _context.RoomId.Returns("currentroom");
        _context.Target.Returns("TargetRoom");

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("html");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(Arg.Any<string>(), Arg.Is<HallOfFameViewModel>(vm =>
            vm.SortedPlayerRecords.Length == 1 &&
            vm.SortedPlayerRecords[0].UserId == "user1" &&
            vm.SortedPlayerRecords[0].UserName == "user1"
        ));
    }
}