using System.Collections.Concurrent;
using System.Globalization;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using ElsaMina.Commands.Dolls;
using ElsaMina.Commands.Profile;
using ElsaMina.Commands.Showdown.Ranking;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Handlers.DefaultHandlers;
using ElsaMina.Core.Services.BattleTracker;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Dispatch;
using ElsaMina.Core.Services.FeatureSwitches;
using ElsaMina.Core.Services.Lifecycle;
using ElsaMina.Core.Services.PrivateMessages;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.RoomInfo;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Scheduling;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Services.Telemetry;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Services.UserData;
using ElsaMina.Core.Services.UserDetails;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NSubstitute;

namespace ElsaMina.IntegrationTests.Commands.Profile;

/// <summary>
/// Runs the profile command through the real pipeline: frame dispatcher, handlers, command executor, profile
/// service and user details manager. A fake server answers the bot's <c>/cmd userdetails</c> queries with
/// <c>queryresponse</c> frames, so the test covers the command waiting for a reply that arrives as a later frame.
/// </summary>
[TestFixture]
public class ProfileCommandIntegrationTest
{
    private const string ROOM_ID = "franais";
    private const string USER_DETAILS_QUERY_PREFIX = "|/cmd userdetails ";
    private static readonly TimeSpan USER_DETAILS_TIMEOUT = TimeSpan.FromSeconds(5);

    private readonly ConcurrentQueue<string> _sentMessages = new();
    private readonly ConcurrentQueue<string> _userDetailsQueries = new();
    private readonly Dictionary<string, string> _userDetailsByUserId = new();
    private bool _isServerAnsweringAutomatically;
    private TaskCompletionSource _userDetailsTimeout;

    private IClient _client;
    private IContainer _container;
    private IIncomingMessageDispatcher _dispatcher;

    [SetUp]
    public void SetUp()
    {
        _sentMessages.Clear();
        _userDetailsQueries.Clear();
        _userDetailsByUserId.Clear();
        _isServerAnsweringAutomatically = true;
        _userDetailsTimeout = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _client = Substitute.For<IClient>();
        _client.When(client => client.Send(Arg.Any<string>())).Do(callInfo => OnClientSend(callInfo.Arg<string>()));

        var builder = new ContainerBuilder();
        RegisterRuntime(builder);
        RegisterProfileFeature(builder);
        _container = builder.Build();

        _container.Resolve<ITemplatesManager>().LoadTemplates();
        _container.Resolve<IHandlerManager>().Initialize();
        _dispatcher = _container.Resolve<IIncomingMessageDispatcher>();

        _userDetailsByUserId["earth"] =
            """{"userid":"earth","name":"Earth","avatar":"1","group":" ","status":"!Busy","rooms":{"+franais":{}}}""";
        _userDetailsByUserId["mec"] =
            """{"userid":"mec","name":"Mec","avatar":"2","group":" ","rooms":{"%franais":{}}}""";
    }

    [TearDown]
    public void TearDown()
    {
        // Lets a query still waiting on the fake timeout end before the container goes away.
        _userDetailsTimeout.TrySetResult();
        _container.Dispose();
        _client.Dispose();
    }

    [Test]
    public async Task Test_ProfileCommand_ShouldRenderShowdownUserDetails_WhenServerAnswersQuery()
    {
        // Act
        await _dispatcher.DispatchAsync($">{ROOM_ID}\n|c:|1700000000|+Earth|-profile");
        var profile = await WaitForProfileAsync("earth");

        // Assert
        Assert.That(_userDetailsQueries, Is.EqualTo(new[] { $"{USER_DETAILS_QUERY_PREFIX}earth" }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(profile, Does.StartWith($"{ROOM_ID}|/adduhtml profile-earth, "));
            Assert.That(profile, Does.Contain("Earth"));
            Assert.That(profile, Does.Contain("sprites/trainers/lucas.png"), "avatar from the user details");
            Assert.That(profile, Does.Contain("Busy"), "status from the user details");
            Assert.That(profile, Does.Contain("rank_voice"), "room rank from the user details");
        }
    }

    [Test]
    public async Task Test_ProfileCommand_ShouldCombineStoredDataWithUserDetails()
    {
        // Arrange
        await SeedRoomUserAsync("earth", "Earth", title: "Champion of the Ladder");

        // Act
        await _dispatcher.DispatchAsync($">{ROOM_ID}\n|c:|1700000000|+Earth|-profile");
        var profile = await WaitForProfileAsync("earth");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(profile, Does.Contain("Champion of the Ladder"), "title from the database");
            Assert.That(profile, Does.Contain("sprites/trainers/lucas.png"), "avatar from the user details");
        }
    }

    [Test]
    public async Task Test_ProfileCommand_ShouldWaitForUserDetails_WhileRoomKeepsBeingHandled()
    {
        // Arrange
        _isServerAnsweringAutomatically = false;

        // Act: the first profile waits for its user details
        await _dispatcher.DispatchAsync($">{ROOM_ID}\n|c:|1700000000|+Earth|-profile");
        await Wait.UntilAsync(() => _userDetailsQueries.Contains($"{USER_DETAILS_QUERY_PREFIX}earth"),
            "the user details query for earth");

        // Assert: nothing is rendered without the details, and the room's next command is still handled
        Assert.That(FindProfile("earth"), Is.Null);
        await _dispatcher.DispatchAsync($">{ROOM_ID}\n|c:|1700000001|+Earth|-profile mec");
        await Wait.UntilAsync(() => _userDetailsQueries.Contains($"{USER_DETAILS_QUERY_PREFIX}mec"),
            "the user details query for mec, sent while earth's profile is still waiting");

        // Act: the server answers in the opposite order
        await AnswerUserDetailsAsync("mec");
        var mecProfile = await WaitForProfileAsync("mec");
        Assert.That(FindProfile("earth"), Is.Null, "earth's profile must keep waiting for its own details");
        await AnswerUserDetailsAsync("earth");
        var earthProfile = await WaitForProfileAsync("earth");

        // Assert: each reply resolved its own query
        using (Assert.EnterMultipleScope())
        {
            Assert.That(mecProfile, Does.Contain("Mec").And.Contain("sprites/trainers/dawn.png"));
            Assert.That(mecProfile, Does.Contain("rank_driver"));
            Assert.That(earthProfile, Does.Contain("Earth").And.Contain("sprites/trainers/lucas.png"));
            Assert.That(earthProfile, Does.Contain("rank_voice"));
        }
    }

    [Test]
    public async Task Test_ProfileCommand_ShouldReceiveUserDetails_WhenRequestedInPrivateMessage()
    {
        // Private messages and query responses both come without a room header, so they share one lane:
        // the reply can only be handled because the command does not hold that lane while it waits.

        // Act
        await _dispatcher.DispatchAsync("|pm|+Earth| Bot|-profile");
        var profile = await WaitForProfileAsync("earth");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(profile, Does.StartWith($"{ROOM_ID}|/pmuhtml earth, profile-earth, "));
            Assert.That(profile, Does.Contain("sprites/trainers/lucas.png"));
            Assert.That(_userDetailsTimeout.Task.IsCompleted, Is.False, "the details must arrive before the timeout");
        }
    }

    [Test]
    public async Task Test_ProfileCommand_ShouldRenderStoredProfile_WhenUserDetailsTimeOut()
    {
        // Arrange
        _isServerAnsweringAutomatically = false;
        await SeedRoomUserAsync("earth", "Earth", title: "Champion of the Ladder");

        // Act
        await _dispatcher.DispatchAsync($">{ROOM_ID}\n|c:|1700000000|+Earth|-profile");
        await Wait.UntilAsync(() => !_userDetailsQueries.IsEmpty, "the user details query");
        await Wait.ForQuietPeriodAsync(TimeSpan.FromMilliseconds(200));
        Assert.That(FindProfile("earth"), Is.Null, "the profile waits for the details until the timeout");
        _userDetailsTimeout.SetResult();
        var profile = await WaitForProfileAsync("earth");

        // Assert: the profile falls back to the stored data and the default avatar
        var defaultAvatar = ProfileService.GetAvatar(null, null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(profile, Does.Contain("Champion of the Ladder"));
            Assert.That(profile, Does.Contain("Earth"));
            Assert.That(profile, Does.Contain(defaultAvatar));
            Assert.That(profile, Does.Not.Contain("rank_voice"));
        }
    }

    [Test]
    public async Task Test_ProfileCommand_ShouldIgnoreUserDetailsOfAnotherUser()
    {
        // Arrange
        _isServerAnsweringAutomatically = false;

        // Act
        await _dispatcher.DispatchAsync($">{ROOM_ID}\n|c:|1700000000|+Earth|-profile");
        await Wait.UntilAsync(() => !_userDetailsQueries.IsEmpty, "the user details query");
        await AnswerUserDetailsAsync("mec");
        await Wait.ForQuietPeriodAsync(TimeSpan.FromMilliseconds(200));

        // Assert
        Assert.That(FindProfile("earth"), Is.Null);
        await AnswerUserDetailsAsync("earth");
        Assert.That(await WaitForProfileAsync("earth"), Does.Contain("sprites/trainers/lucas.png"));
    }

    private void RegisterRuntime(ContainerBuilder builder)
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Bot");
        configuration.Trigger.Returns("-");
        configuration.DefaultRoom.Returns(ROOM_ID);
        configuration.DefaultLocaleCode.Returns("en-US");
        configuration.RoomBlacklist.Returns([]);
        configuration.Whitelist.Returns([]);

        var room = Substitute.For<IRoom>();
        room.RoomId.Returns(ROOM_ID);
        room.Culture.Returns(new CultureInfo("en-US"));
        room.TimeZone.Returns(TimeZoneInfo.Utc);
        room.Users.Returns(new Dictionary<string, IUser>
        {
            ["earth"] = new User("Earth", Rank.Voiced),
            ["mec"] = new User("Mec", Rank.Regular)
        });

        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.HasRoom(ROOM_ID).Returns(true);
        roomsManager.GetRoom(ROOM_ID).Returns(room);

        var resourcesService = Substitute.For<IResourcesService>();
        resourcesService.GetString(Arg.Any<string>(), Arg.Any<CultureInfo>())
            .Returns(callInfo => callInfo.Arg<string>());

        var userColorsService = Substitute.For<IUserColorsService>();
        userColorsService.GetUserColor(Arg.Any<string>()).Returns("#000000");

        builder.RegisterInstance(_client).As<IClient>();
        builder.RegisterInstance(configuration).As<IConfiguration>();
        builder.RegisterInstance(roomsManager).As<IRoomsManager>();
        builder.RegisterInstance(resourcesService).As<IResourcesService>();
        builder.RegisterInstance(userColorsService).As<IUserColorsService>();
        builder.RegisterInstance(CreateSystemService()).As<ISystemService>();
        builder.RegisterInstance(Substitute.For<ITelemetryService>()).As<ITelemetryService>();
        builder.RegisterInstance(Substitute.For<IBotLifecycleService>()).As<IBotLifecycleService>();
        builder.RegisterInstance(Substitute.For<IActiveBattlesManager>()).As<IActiveBattlesManager>();
        builder.RegisterInstance(Substitute.For<IRoomInfoManager>()).As<IRoomInfoManager>();
        builder.RegisterType<ClockService>().As<IClockService>().SingleInstance();
        builder.Register(context => new AutofacServiceProvider(context.Resolve<ILifetimeScope>()))
            .As<IServiceProvider>().SingleInstance();
        builder.RegisterType<TemplatesManager>().As<ITemplatesManager>().SingleInstance();

        builder.RegisterType<KeyedTaskQueue>().As<IKeyedTaskQueue>();
        builder.RegisterType<IncomingMessageDispatcher>().As<IIncomingMessageDispatcher>().SingleInstance();
        builder.RegisterType<OutgoingMessageQueue>().As<IOutgoingMessageQueue>().SingleInstance();
        builder.RegisterType<HandlerManager>().As<IHandlerManager>().SingleInstance();
        builder.RegisterType<Bot>().As<IBot>().SingleInstance();
        builder.RegisterType<PmSendersManager>().As<IPmSendersManager>().SingleInstance();
        builder.RegisterType<ContextFactory>().As<IContextFactory>().SingleInstance();
        builder.RegisterType<FeatureSwitchService>().As<IFeatureSwitchService>().SingleInstance();
        builder.RegisterType<CommandRegistry>().As<ICommandRegistry>().SingleInstance();
        builder.RegisterType<CommandExecutor>().As<ICommandExecutor>().SingleInstance();
        builder.RegisterType<UserDetailsManager>().As<IUserDetailsManager>().SingleInstance();
        builder.RegisterHandler<ChatMessageCommandHandler>();
        builder.RegisterHandler<PrivateMessageCommandHandler>();
        builder.RegisterHandler<QueryResponseHandler>();
    }

    private static void RegisterProfileFeature(ContainerBuilder builder)
    {
        var dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var userDataService = Substitute.For<IUserDataService>();
        userDataService.GetRegisterDateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DateTimeOffset(2015, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var dollService = Substitute.For<IDollService>();
        dollService.ResolveDollsAsync(Arg.Any<IEnumerable<DollHolding>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        builder.RegisterInstance(new BotDbContextFactory(new PooledDbContextFactory<BotDbContext>(dbOptions)))
            .As<IBotDbContextFactory>();
        builder.RegisterInstance(userDataService).As<IUserDataService>();
        builder.RegisterInstance(Substitute.For<IBestRankingProvider>()).As<IBestRankingProvider>();
        builder.RegisterInstance(dollService).As<IDollService>();
        builder.RegisterType<ProfileService>().As<IProfileService>().SingleInstance();
        builder.RegisterCommand<ProfileCommand>();
    }

    /// <summary>
    /// Real waits for the outgoing message cooldown, and a timeout the test controls for the user details query,
    /// so a test can make the query time out without waiting five seconds.
    /// </summary>
    private ISystemService CreateSystemService()
    {
        var systemService = Substitute.For<ISystemService>();
        systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(callInfo =>
        {
            var delay = callInfo.Arg<TimeSpan>();
            var cancellationToken = callInfo.Arg<CancellationToken>();
            return delay >= USER_DETAILS_TIMEOUT
                ? _userDetailsTimeout.Task.WaitAsync(cancellationToken)
                : Task.Delay(delay, cancellationToken);
        });
        return systemService;
    }

    private void OnClientSend(string message)
    {
        if (!message.StartsWith(USER_DETAILS_QUERY_PREFIX))
        {
            _sentMessages.Enqueue(message);
            return;
        }

        _userDetailsQueries.Enqueue(message);
        if (_isServerAnsweringAutomatically)
        {
            // The server answers asynchronously, as a later frame.
            _ = Task.Run(() => AnswerUserDetailsAsync(message[USER_DETAILS_QUERY_PREFIX.Length..]));
        }
    }

    private Task AnswerUserDetailsAsync(string userId)
    {
        return _dispatcher.DispatchAsync($"|queryresponse|userdetails|{_userDetailsByUserId[userId]}");
    }

    private async Task SeedRoomUserAsync(string userId, string userName, string title)
    {
        await using var dbContext = await _container.Resolve<IBotDbContextFactory>().CreateDbContextAsync();
        dbContext.Users.Add(new SavedUser { UserId = userId, UserName = userName });
        dbContext.RoomUsers.Add(new RoomUser { Id = userId, RoomId = ROOM_ID, Title = title });
        await dbContext.SaveChangesAsync();
    }

    private string FindProfile(string userId)
    {
        return _sentMessages.FirstOrDefault(message => message.Contains($"profile-{userId},"));
    }

    private async Task<string> WaitForProfileAsync(string userId)
    {
        await Wait.UntilAsync(() => FindProfile(userId) != null, $"the profile of {userId}");
        return FindProfile(userId);
    }
}
