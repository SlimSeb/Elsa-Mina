using ElsaMina.Commands.Watchlist;
using ElsaMina.Core;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.CustomColors;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.System;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Watchlist;

public class WatchlistServiceTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private IBot _bot;
    private IHttpService _httpService;
    private ISystemService _systemService;
    private IConfiguration _configuration;
    private IUserColorsService _userColorsService;
    private WatchlistService _sut;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => new BotDbContext(_dbOptions));

        _bot = Substitute.For<IBot>();
        _httpService = Substitute.For<IHttpService>();
        _systemService = Substitute.For<ISystemService>();
        _configuration = Substitute.For<IConfiguration>();
        _userColorsService = Substitute.For<IUserColorsService>();

        // By default, the staff intro timeout never elapses unless the request gets cancelled.
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.Delay(Timeout.Infinite, callInfo.ArgAt<CancellationToken>(1)));
        _configuration.DiscordWebhooks.Returns(new Dictionary<string, string>());
        _userColorsService.GetUserColor(Arg.Any<string>()).Returns("#123456");

        _sut = new WatchlistService(_dbContextFactory, _bot, _httpService, _systemService, _configuration,
            _userColorsService);
    }

    private async Task SeedEntryAsync(string roomId, string userId, string rank)
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        dbContext.WatchlistEntries.Add(new WatchlistEntry { RoomId = roomId, UserId = userId, Rank = rank });
        await dbContext.SaveChangesAsync();
    }

    private async Task<List<WatchlistEntry>> GetAllEntriesAsync()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        return await dbContext.WatchlistEntries.ToListAsync();
    }

    [Test]
    public async Task Test_GetWatchlistAsync_ShouldReturnOnlyEntriesOfRequestedRoom()
    {
        await SeedEntryAsync("room", "alice", "%");
        await SeedEntryAsync("room", "bob", "@");
        await SeedEntryAsync("otherroom", "carol", "+");

        var watchlist = await _sut.GetWatchlistAsync("room");

        Assert.That(watchlist, Is.EquivalentTo(new Dictionary<string, string> { ["alice"] = "%", ["bob"] = "@" }));
    }

    [Test]
    public async Task Test_AddToWatchlistAsync_ShouldCreateEntry_WhenUserIsNotInWatchlist()
    {
        await _sut.AddToWatchlistAsync("room", "alice", "%");

        var entries = await GetAllEntriesAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entries[0].RoomId, Is.EqualTo("room"));
            Assert.That(entries[0].UserId, Is.EqualTo("alice"));
            Assert.That(entries[0].Rank, Is.EqualTo("%"));
        }
    }

    [Test]
    public async Task Test_AddToWatchlistAsync_ShouldUpdateRank_WhenUserIsAlreadyInWatchlist()
    {
        await SeedEntryAsync("room", "alice", "+");

        await _sut.AddToWatchlistAsync("room", "alice", "%");

        var entries = await GetAllEntriesAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Rank, Is.EqualTo("%"));
    }

    [Test]
    public async Task Test_RemoveFromWatchlistAsync_ShouldReturnFalse_WhenEntryDoesNotExist()
    {
        var result = await _sut.RemoveFromWatchlistAsync("room", "alice", "%");

        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Test_RemoveFromWatchlistAsync_ShouldReturnFalseAndKeepEntry_WhenRankDoesNotMatch()
    {
        await SeedEntryAsync("room", "alice", "%");

        var result = await _sut.RemoveFromWatchlistAsync("room", "alice", "@");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.False);
            Assert.That(await GetAllEntriesAsync(), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Test_RemoveFromWatchlistAsync_ShouldRemoveEntryAndReturnTrue_WhenRankMatches()
    {
        await SeedEntryAsync("room", "alice", "%");

        var result = await _sut.RemoveFromWatchlistAsync("room", "alice", "%");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.True);
            Assert.That(await GetAllEntriesAsync(), Is.Empty);
        }
    }

    [Test]
    public async Task Test_FetchAndUpdateStaffIntroAsync_ShouldReplaceWatchlistDiv_WhenStaffIntroIsReceived()
    {
        await SeedEntryAsync("room", "alice", "%");

        var fetchTask = _sut.FetchAndUpdateStaffIntroAsync("room");
        _sut.HandleReceivedStaffIntro("room",
            """<div class="infobox"><p>Intro</p><div class="watchlist">old</div></div>""");
        await fetchTask;

        Received.InOrder(() =>
        {
            _bot.Say("room", "/staffintro");
            _bot.Say("room",
                """/staffintro <p>Intro</p><div class="watchlist"> <strong class="username alice" style="color: #123456;">%alice</strong></div>""");
        });
    }

    [Test]
    public async Task Test_FetchAndUpdateStaffIntroAsync_ShouldNotUpdateStaffIntro_WhenFetchTimesOut()
    {
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await _sut.FetchAndUpdateStaffIntroAsync("room");

        _bot.Received(1).Say("room", "/staffintro");
        _bot.DidNotReceive().Say("room", Arg.Is<string>(message => message.StartsWith("/staffintro ")));
    }

    [Test]
    public async Task Test_SendDiscordNotificationAsync_ShouldNotSendRequest_WhenRoomHasNoWebhook()
    {
        await _sut.SendDiscordNotificationAsync("room", "message");

        await _httpService.DidNotReceiveWithAnyArgs().SendAsync<object>(default);
    }

    [Test]
    public async Task Test_SendDiscordNotificationAsync_ShouldPostToWebhook_WhenRoomHasWebhook()
    {
        _configuration.DiscordWebhooks.Returns(new Dictionary<string, string>
        {
            ["room"] = "https://discord.example/webhook"
        });

        await _sut.SendDiscordNotificationAsync("room", "message");

        await _httpService.Received(1).SendAsync<object>(
            Arg.Is<HttpRequest>(request => request.Method == HttpMethod.Post
                                           && request.Uri == "https://discord.example/webhook"
                                           && request.Body != null),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public void Test_SendDiscordNotificationAsync_ShouldNotThrow_WhenRequestFails()
    {
        _configuration.DiscordWebhooks.Returns(new Dictionary<string, string>
        {
            ["room"] = "https://discord.example/webhook"
        });
        _httpService.SendAsync<object>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("boom"));

        Assert.DoesNotThrowAsync(() => _sut.SendDiscordNotificationAsync("room", "message"));
    }
}
