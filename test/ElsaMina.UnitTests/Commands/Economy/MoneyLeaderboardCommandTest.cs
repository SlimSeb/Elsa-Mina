using ElsaMina.Commands.Economy;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Economy;

public class MoneyLeaderboardCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private IRoom _room;
    private MoneyLeaderboardCommand _command;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _templatesManager = Substitute.For<ITemplatesManager>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();

        _context.Room.Returns(_room);
        _context.RoomId.Returns("room");
        _room.GetParameterValueAsync(EconomyRoomParameters.BucksEnabled, Arg.Any<CancellationToken>()).Returns("true");

        _command = new MoneyLeaderboardCommand(_dbContextFactory, _templatesManager);
    }

    private async Task SeedRoomUsersAsync(params RoomUser[] roomUsers)
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        dbContext.RoomUsers.AddRange(roomUsers);
        await dbContext.SaveChangesAsync();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyBucksDisabled_WhenBucksAreDisabled()
    {
        _room.GetParameterValueAsync(EconomyRoomParameters.BucksEnabled, Arg.Any<CancellationToken>()).Returns("false");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("bucks_disabled");
        await _templatesManager.DidNotReceiveWithAnyArgs().GetTemplateAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyEmpty_WhenNoUserHasMoreThanDefaultBalance()
    {
        await SeedRoomUsersAsync(
            new RoomUser { Id = "alice", RoomId = "room", Money = IMoneyService.DEFAULT_BALANCE },
            new RoomUser { Id = "bob", RoomId = "otherroom", Money = 5000 });

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("money_leaderboard_empty");
        await _templatesManager.DidNotReceiveWithAnyArgs().GetTemplateAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderRoomLeaderboardOrderedByMoney_WhenRichUsersExist()
    {
        await SeedRoomUsersAsync(
            new RoomUser { Id = "alice", RoomId = "room", Money = 300 },
            new RoomUser { Id = "bob", RoomId = "room", Money = 900 },
            new RoomUser { Id = "carol", RoomId = "room", Money = 50 },
            new RoomUser { Id = "dave", RoomId = "otherroom", Money = 5000 });
        _templatesManager.GetTemplateAsync("Economy/MoneyLeaderboard", Arg.Any<MoneyLeaderboardViewModel>())
            .Returns("<div>\nleaderboard\n</div>");

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync("Economy/MoneyLeaderboard",
            Arg.Is<MoneyLeaderboardViewModel>(viewModel =>
                viewModel.Leaderboard.Count == 2
                && viewModel.Leaderboard[0].Key == "bob" && viewModel.Leaderboard[0].Value == 900
                && viewModel.Leaderboard[1].Key == "alice" && viewModel.Leaderboard[1].Value == 300));
        _context.Received(1).Reply("/addhtmlbox <div>leaderboard</div>");
    }

    [Test]
    public async Task Test_RunAsync_ShouldLimitLeaderboardToTwentyEntries()
    {
        var roomUsers = Enumerable.Range(1, 25)
            .Select(index => new RoomUser { Id = $"user{index}", RoomId = "room", Money = 100 + index })
            .ToArray();
        await SeedRoomUsersAsync(roomUsers);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns(string.Empty);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync("Economy/MoneyLeaderboard",
            Arg.Is<MoneyLeaderboardViewModel>(viewModel => viewModel.Leaderboard.Count == 20));
    }
}
