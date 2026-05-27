using System.Globalization;
using ElsaMina.Commands.Users;
using ElsaMina.Core.Contexts;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Users;

public class SeenCommandTest
{
    private static DbContextOptions<BotDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static IBotDbContextFactory CreateFactoryReturning(BotDbContext context)
    {
        var factory = Substitute.For<IBotDbContextFactory>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(context);
        return factory;
    }

    private static async Task SeedUserAsync(DbContextOptions<BotDbContext> options, SavedUser user)
    {
        await using var setup = new BotDbContext(options);
        await setup.Database.EnsureCreatedAsync();
        setup.Users.Add(user);
        await setup.SaveChangesAsync();
    }

    [Test]
    public async Task Test_IsWhiteListOnly_ShouldBeTrue()
    {
        // Arrange & Act
        var options = CreateOptions();
        await using var db = new BotDbContext(options);
        var command = new SeenCommand(CreateFactoryReturning(db));
        
        // Assert
        Assert.That(command.IsWhitelistOnly, Is.True);
    }

    [Test]
    public async Task RunAsync_ShouldReplyHelp_WhenTargetIsMissing()
    {
        // Arrange
        var options = CreateOptions();
        await using var db = new BotDbContext(options);
        var command = new SeenCommand(CreateFactoryReturning(db));
        var context = Substitute.For<IContext>();
        context.Target.ReturnsNull();
        context.GetString("seen_command_help").Returns("seen help");

        // Act
        await command.RunAsync(context);

        // Assert
        context.Received(1).GetString("seen_command_help");
        context.Received(1).Reply("seen help", rankAware: true);
        context.DidNotReceiveWithAnyArgs()
            .ReplyRankAwareLocalizedMessage(null!, null!);
    }

    [Test]
    public async Task RunAsync_ShouldReplyError_WhenDatabaseThrows()
    {
        // Arrange
        const string target = "Alice";
        var factory = Substitute.For<IBotDbContextFactory>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Throws(_ => throw new InvalidOperationException("db down"));

        var command = new SeenCommand(factory);
        var context = Substitute.For<IContext>();
        context.Room.TimeZone.Returns(TimeZoneInfo.Utc);
        context.Target.Returns(target);

        // Act
        await command.RunAsync(context);

        // Assert
        context.Received(1)
            .ReplyRankAwareLocalizedMessage("seen_command_error", "alice", "db down");
    }

    [Test]
    public async Task RunAsync_ShouldReplyNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        var options = CreateOptions();
        await using var db = new BotDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var command = new SeenCommand(CreateFactoryReturning(db));

        var context = Substitute.For<IContext>();
        context.Room.TimeZone.Returns(TimeZoneInfo.Utc);
        context.Target.Returns("alice");

        // Act
        await command.RunAsync(context);

        // Assert
        context.Received(1).ReplyRankAwareLocalizedMessage("seen_command_not_found", "alice");
    }

    [Test]
    [TestCase(UserAction.Joining, "seen_command_action_joining", "joining")]
    [TestCase(UserAction.Leaving, "seen_command_action_leaving", "leaving")]
    [TestCase(UserAction.Chatting, "seen_command_action_chatting", "chatting in")]
    public async Task RunAsync_ShouldReplyLastSeenMessage_ForKnownActions(
        UserAction action,
        string actionStringId,
        string translatedAction)
    {
        // Arrange
        var options = CreateOptions();
        await SeedUserAsync(options, new SavedUser
        {
            UserId = "alice",
            UserName = "Alice",
            LastOnline = new DateTimeOffset(2026, 2, 19, 15, 30, 0, TimeSpan.Zero),
            LastSeenAction = action,
            LastSeenRoomId = "lobby"
        });

        await using var db = new BotDbContext(options);
        var command = new SeenCommand(CreateFactoryReturning(db));

        var context = Substitute.For<IContext>();
        context.Room.TimeZone.Returns(TimeZoneInfo.Utc);
        context.Target.Returns("alice");
        context.Culture.Returns(new CultureInfo("en-US"));
        context.GetString(actionStringId).Returns(translatedAction);

        // Act
        await command.RunAsync(context);

        // Assert
        context.Received(1)
            .ReplyRankAwareLocalizedMessage("seen_command_last_seen", "Alice", Arg.Any<string>(), translatedAction, "lobby");
    }

    [Test]
    public async Task RunAsync_ShouldUseTargetAsFallback_WhenStoredUserNameIsMissing()
    {
        // Arrange
        var options = CreateOptions();
        await SeedUserAsync(options, new SavedUser
        {
            UserId = "alice",
            UserName = " ",
            LastOnline = new DateTimeOffset(2026, 2, 19, 15, 30, 0, TimeSpan.Zero),
            LastSeenAction = UserAction.Chatting,
            LastSeenRoomId = "lobby"
        });

        await using var db = new BotDbContext(options);
        var command = new SeenCommand(CreateFactoryReturning(db));

        var context = Substitute.For<IContext>();
        context.Room.TimeZone.Returns(TimeZoneInfo.Utc);
        context.Target.Returns("alice");
        context.Culture.Returns(new CultureInfo("en-US"));
        context.GetString("seen_command_action_chatting").Returns("chatting in");

        // Act
        await command.RunAsync(context);

        // Assert
        context.Received(1)
            .ReplyRankAwareLocalizedMessage("seen_command_last_seen", "alice", Arg.Any<string>(), "chatting in", "lobby");
    }

    [Test]
    [TestCase(UserAction.Unknown, true, "lobby", Description = "Unknown action")]
    [TestCase(UserAction.Joining, false, "lobby", Description = "No LastOnline")]
    [TestCase(UserAction.Joining, true, null, Description = "No room id")]
    [TestCase(UserAction.Joining, true, "   ", Description = "Whitespace room id")]
    public async Task RunAsync_ShouldReplyNotFound_WhenLastSeenDataIsIncomplete(
        UserAction action,
        bool hasLastOnline,
        string lastSeenRoomId)
    {
        // Arrange
        var options = CreateOptions();
        await SeedUserAsync(options, new SavedUser
        {
            UserId = "alice",
            UserName = "Alice",
            LastSeenAction = action,
            LastOnline = hasLastOnline
                ? new DateTimeOffset(2026, 2, 19, 15, 30, 0, TimeSpan.Zero)
                : null,
            LastSeenRoomId = lastSeenRoomId
        });

        await using var db = new BotDbContext(options);
        var command = new SeenCommand(CreateFactoryReturning(db));

        var context = Substitute.For<IContext>();
        context.Target.Returns("alice");

        // Act
        await command.RunAsync(context);

        // Assert
        context.Received(1).ReplyRankAwareLocalizedMessage("seen_command_not_found", "alice");
    }
}
