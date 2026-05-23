using System.Globalization;
using ElsaMina.Commands.Tournaments.Betting.Leaderboard;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace ElsaMina.UnitTests.Commands.Tournaments.Betting.Leaderboard;

[TestFixture]
public class TopBettorsCommandTest
{
    private IContext _context;
    private ITemplatesManager _templatesManager;
    private IBotDbContextFactory _dbContextFactory;
    private IRoomsManager _roomsManager;
    private TopBettorsCommand _command;
    private DbContextOptions<BotDbContext> _dbOptions;

    private static async Task SeedBettorAsync(BotDbContext dbContext, string userId, string roomId,
        int correctBetsCount = 0, int totalBetsCount = 0, string userName = null)
    {
        var user = await dbContext.Users.FindAsync([userId]);
        if (user == null)
        {
            user = new SavedUser { UserId = userId, UserName = userName ?? userId };
            await dbContext.Users.AddAsync(user);
        }

        var roomUser = await dbContext.RoomUsers.FindAsync([userId, roomId]);
        if (roomUser == null)
        {
            roomUser = new RoomUser { Id = userId, RoomId = roomId };
            await dbContext.RoomUsers.AddAsync(roomUser);
        }

        await dbContext.BetRecords.AddAsync(new BetRecord
        {
            UserId = userId,
            RoomId = roomId,
            CorrectBetsCount = correctBetsCount,
            TotalBetsCount = totalBetsCount
        });
    }

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _context.Target.Returns(string.Empty);
        _context.RoomId.Returns("arcade");
        _context.Culture.Returns(CultureInfo.InvariantCulture);

        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<div/>");

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));

        _roomsManager = Substitute.For<IRoomsManager>();
        _roomsManager.GetRoom(Arg.Any<string>()).Returns((IRoom)null);

        _command = new TopBettorsCommand(_dbContextFactory, _templatesManager, _roomsManager);
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
    public async Task Test_RunAsync_ShouldReplyNoData_WhenNoRecordsExist()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("top_bettors_no_data");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenDatabaseThrows()
    {
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("db error"));

        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("top_bettors_error");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallTemplateWithCorrectKey_WhenRecordsExist()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await SeedBettorAsync(dbContext, "alice", "arcade", correctBetsCount: 3, totalBetsCount: 5);
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Tournaments/Betting/Leaderboard/TopBettorsTable",
            Arg.Any<TopBettorsViewModel>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCorrectViewModel_WhenRecordsExist()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await SeedBettorAsync(dbContext, "alice", "arcade", correctBetsCount: 3, totalBetsCount: 5);
        await SeedBettorAsync(dbContext, "bob", "arcade", correctBetsCount: 1, totalBetsCount: 4);
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TopBettorsViewModel>(vm =>
                vm.Room == "arcade" &&
                vm.Culture.Name == "" &&
                vm.TopList.Count() == 2));
    }

    [Test]
    public async Task Test_RunAsync_ShouldExcludeRecordsFromOtherRooms()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await SeedBettorAsync(dbContext, "alice", "arcade", correctBetsCount: 5, totalBetsCount: 5);
        await SeedBettorAsync(dbContext, "bob", "otherroom", correctBetsCount: 10, totalBetsCount: 10);
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TopBettorsViewModel>(vm =>
                vm.TopList.Count() == 1 &&
                vm.TopList.First().UserId == "alice"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldOrderByCorrectBetsCountDescending()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await SeedBettorAsync(dbContext, "alice", "arcade", correctBetsCount: 1, totalBetsCount: 5);
        await SeedBettorAsync(dbContext, "charlie", "arcade", correctBetsCount: 5, totalBetsCount: 5);
        await SeedBettorAsync(dbContext, "bob", "arcade", correctBetsCount: 3, totalBetsCount: 5);
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TopBettorsViewModel>(vm =>
                vm.TopList.ElementAt(0).UserId == "charlie" && vm.TopList.ElementAt(0).Rank == 1 &&
                vm.TopList.ElementAt(1).UserId == "bob" && vm.TopList.ElementAt(1).Rank == 2 &&
                vm.TopList.ElementAt(2).UserId == "alice" && vm.TopList.ElementAt(2).Rank == 3));
    }

    [Test]
    public async Task Test_RunAsync_ShouldOrderByTotalBetsCountDescending_WhenCorrectBetsAreTied()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await SeedBettorAsync(dbContext, "alice", "arcade", correctBetsCount: 3, totalBetsCount: 4);
        await SeedBettorAsync(dbContext, "bob", "arcade", correctBetsCount: 3, totalBetsCount: 7);
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TopBettorsViewModel>(vm =>
                vm.TopList.ElementAt(0).UserId == "bob" &&
                vm.TopList.ElementAt(1).UserId == "alice"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldLimitResultsToThirty()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        foreach (var index in Enumerable.Range(1, 40))
        {
            await SeedBettorAsync(dbContext, $"user{index}", "arcade",
                correctBetsCount: index, totalBetsCount: index);
        }
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TopBettorsViewModel>(vm => vm.TopList.Count() == 30));
    }

    [Test]
    public async Task Test_RunAsync_ShouldFallbackToUserId_WhenUserNameIsNull()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await SeedBettorAsync(dbContext, "orphan", "arcade",
            correctBetsCount: 1, totalBetsCount: 1, userName: null);
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TopBettorsViewModel>(vm =>
                vm.TopList.First().UserName == "orphan"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseUserNameFromRoomUser_WhenPresent()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await SeedBettorAsync(dbContext, "alice", "arcade",
            correctBetsCount: 2, totalBetsCount: 3, userName: "Alice");
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TopBettorsViewModel>(vm =>
                vm.TopList.First().UserName == "Alice"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTargetRoomId_WhenTargetIsProvided()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await SeedBettorAsync(dbContext, "alice", "otherroom",
            correctBetsCount: 2, totalBetsCount: 3);
        await dbContext.SaveChangesAsync();

        _context.Target.Returns("OtherRoom");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TopBettorsViewModel>(vm =>
                vm.TopList.Count() == 1 &&
                vm.TopList.First().UserId == "alice"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseRoomNameFromRoomsManager_WhenRoomExists()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await SeedBettorAsync(dbContext, "alice", "arcade", correctBetsCount: 1, totalBetsCount: 1);
        await dbContext.SaveChangesAsync();

        var room = Substitute.For<IRoom>();
        room.Name.Returns("Arcade");
        _roomsManager.GetRoom("arcade").Returns(room);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TopBettorsViewModel>(vm => vm.Room == "Arcade"));
    }
}
