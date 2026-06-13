using ElsaMina.Commands.Games.VoltorbFlip;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.VoltorbFlip;

public class VoltorbFlipLeaderboardCommandTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private VoltorbFlipLeaderboardCommand _command;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new BotDbContext(_dbOptions);
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => new BotDbContext(_dbOptions));

        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<html>template</html>");

        _context = Substitute.For<IContext>();

        _command = new VoltorbFlipLeaderboardCommand(_dbContextFactory, _templatesManager);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command, Is.Not.Null);
            Assert.That(_command.Name, Is.EqualTo("vfleaderboard"));
            Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
            Assert.That(_command.HelpMessageKey, Is.EqualTo("voltorbflip_leaderboard_help"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyEmpty_WhenNoDataExists()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyRankAwareLocalizedMessage("voltorbflip_leaderboard_empty");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDisplayLeaderboard_WhenDataExists()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.Users.AddAsync(new SavedUser { UserId = "user1", UserName = "User1" });
            await setupContext.Users.AddAsync(new SavedUser { UserId = "user2", UserName = "User2" });
            await setupContext.Users.AddAsync(new SavedUser { UserId = "user3", UserName = "User3" });
            await setupContext.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel { UserId = "user1", Level = 3, MaxLevel = 5, Coins = 150 });
            await setupContext.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel { UserId = "user2", Level = 5, MaxLevel = 7, Coins = 300 });
            await setupContext.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel { UserId = "user3", Level = 1, MaxLevel = 3, Coins = 50 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Games/VoltorbFlip/VoltorbFlipLeaderboard",
            Arg.Is<VoltorbFlipLeaderboardViewModel>(vm => vm.Leaderboard.Count == 3)
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldOrderByCoinsDescending_WhenDisplayingLeaderboard()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.Users.AddAsync(new SavedUser { UserId = "user1", UserName = "User1" });
            await setupContext.Users.AddAsync(new SavedUser { UserId = "user2", UserName = "User2" });
            await setupContext.Users.AddAsync(new SavedUser { UserId = "user3", UserName = "User3" });
            await setupContext.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel { UserId = "user1", Level = 2, MaxLevel = 2, Coins = 100 });
            await setupContext.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel { UserId = "user2", Level = 5, MaxLevel = 5, Coins = 500 });
            await setupContext.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel { UserId = "user3", Level = 3, MaxLevel = 3, Coins = 250 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Games/VoltorbFlip/VoltorbFlipLeaderboard",
            Arg.Is<VoltorbFlipLeaderboardViewModel>(vm =>
                vm.Leaderboard[0].UserId == "user2" &&
                vm.Leaderboard[1].UserId == "user3" &&
                vm.Leaderboard[2].UserId == "user1"
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldLimitToTwentyEntries_WhenMoreThanTwentyUsersExist()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            for (int i = 1; i <= 25; i++)
            {
                await setupContext.Users.AddAsync(new SavedUser { UserId = $"user{i}", UserName = $"User{i}" });
                await setupContext.VoltorbFlipLevels.AddAsync(
                    new VoltorbFlipLevel { UserId = $"user{i}", Level = i, MaxLevel = i, Coins = i * 10 });
            }

            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Games/VoltorbFlip/VoltorbFlipLeaderboard",
            Arg.Is<VoltorbFlipLeaderboardViewModel>(vm => vm.Leaderboard.Count == 20)
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtml_WhenSuccessful()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.Users.AddAsync(new SavedUser { UserId = "user1", UserName = "User1" });
            await setupContext.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel { UserId = "user1", Level = 3, MaxLevel = 3, Coins = 100 });
            await setupContext.SaveChangesAsync();
        }

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<b>Leaderboard</b>");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyHtml("<b>Leaderboard</b>", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCancellationToken_WhenProvided()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.Users.AddAsync(new SavedUser { UserId = "user1", UserName = "User1" });
            await setupContext.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel { UserId = "user1", Level = 1, MaxLevel = 1, Coins = 10 });
            await setupContext.SaveChangesAsync();
        }

        var cancellationToken = CancellationToken.None;

        await _command.RunAsync(_context, cancellationToken);

        await _dbContextFactory.Received(1).CreateDbContextAsync(cancellationToken);
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCultureToViewModel_WhenDisplayingLeaderboard()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.Users.AddAsync(new SavedUser { UserId = "user1", UserName = "User1" });
            await setupContext.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel { UserId = "user1", Level = 2, MaxLevel = 2, Coins = 100 });
            await setupContext.SaveChangesAsync();
        }

        var expectedCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
        _context.Culture.Returns(expectedCulture);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Games/VoltorbFlip/VoltorbFlipLeaderboard",
            Arg.Is<VoltorbFlipLeaderboardViewModel>(vm => vm.Culture == expectedCulture)
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleError_WhenDatabaseFails()
    {
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BotDbContext>(new Exception("Database error")));

        var action = async () => await _command.RunAsync(_context);

        Assert.That(action, Throws.TypeOf<Exception>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldHandleSingleEntry_WhenOnlyOneUserExists()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.Users.AddAsync(new SavedUser { UserId = "onlyuser", UserName = "OnlyUser" });
            await setupContext.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel { UserId = "onlyuser", Level = 7, MaxLevel = 8, Coins = 999 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Games/VoltorbFlip/VoltorbFlipLeaderboard",
            Arg.Is<VoltorbFlipLeaderboardViewModel>(vm =>
                vm.Leaderboard.Count == 1 &&
                vm.Leaderboard[0].UserId == "onlyuser" &&
                vm.Leaderboard[0].Level == 7 &&
                vm.Leaderboard[0].MaxLevel == 8 &&
                vm.Leaderboard[0].Coins == 999
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeMaxLevel_InLeaderboardEntries()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.Users.AddAsync(new SavedUser { UserId = "user1", UserName = "User1" });
            await setupContext.Users.AddAsync(new SavedUser { UserId = "user2", UserName = "User2" });
            await setupContext.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel { UserId = "user1", Level = 3, MaxLevel = 6, Coins = 200 });
            await setupContext.VoltorbFlipLevels.AddAsync(new VoltorbFlipLevel { UserId = "user2", Level = 1, MaxLevel = 8, Coins = 400 });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Games/VoltorbFlip/VoltorbFlipLeaderboard",
            Arg.Is<VoltorbFlipLeaderboardViewModel>(vm =>
                vm.Leaderboard[0].MaxLevel == 8 &&
                vm.Leaderboard[1].MaxLevel == 6
            )
        );
    }
}
