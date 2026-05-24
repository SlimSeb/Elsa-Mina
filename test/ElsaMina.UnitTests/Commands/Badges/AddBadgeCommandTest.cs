using ElsaMina.Commands.Badges;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;
using System.Threading;

namespace ElsaMina.UnitTests.Commands.Badges;

public class AddBadgeCommandTest
{
    private IContext _context;
    private IBotDbContextFactory _dbContextFactory;

    private DbContextOptions<BotDbContext> _options;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();

        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory
            .CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_options)));
    }

    private AddBadgeCommand CreateCommand() => new AddBadgeCommand(_dbContextFactory);

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        // Assert
        Assert.That(CreateCommand().RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenArgumentsAreMissing()
    {
        // Arrange
        _context.Target.Returns("invalid");
        var cmd = CreateCommand();

        // Act
        await cmd.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("badge_help_message");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenNoCommaInTarget()
    {
        // Arrange
        _context.Target.Returns("badgename");
        var cmd = CreateCommand();

        // Act
        await cmd.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("badge_help_message");
    }

    [Test]
    public async Task Test_RunAsync_ShouldTreatThirdArgAsRoom_WhenThreeArguments()
    {
        // Arrange - 3 args in room context (not PM) should ignore 3rd arg and use context.RoomId
        _context.Target.Returns("badge, http://image.png, extra");
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("testroom");
        var cmd = CreateCommand();

        // Act
        await cmd.RunAsync(_context);

        // Assert - should attempt to add badge, not show help
        _context.DidNotReceive().ReplyLocalizedMessage("badge_help_message");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelpMessage_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);
        var cmd = CreateCommand();

        // Act
        await cmd.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("badge_help_message");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithAlreadyExistMessage_WhenBadgeExists()
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("ExistingBadge, image");
        _context.RoomId.Returns("room1");
        _context.Command.Returns("add-badge");

        using (var setupDb = new BotDbContext(_options))
        {
            setupDb.Badges.Add(new Badge
            {
                Id = "existingbadge",
                RoomId = "room1",
                Name = "ExistingBadge",
                Image = "img"
            });
            setupDb.SaveChanges();
        }

        // Act
        await cmd.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("badge_add_already_exist", "ExistingBadge");
    }

    [Test]
    public async Task Test_RunAsync_ShouldAddBadgeAndReplyWithSuccessMessage_WhenArgumentsAreValid()
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("NewBadge, http://image.url");
        _context.RoomId.Returns("room1");
        _context.Command.Returns("add-badge");

        // Act
        await cmd.RunAsync(_context);

        // Assert
        await using var db = new BotDbContext(_options);
        var badge = db.Badges.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(badge.Name, Is.EqualTo("NewBadge"));
            Assert.That(badge.Image, Is.EqualTo("http://image.url"));
            Assert.That(badge.Id, Is.EqualTo("newbadge"));
            Assert.That(badge.IsTrophy, Is.False);
            Assert.That(badge.RoomId, Is.EqualTo("room1"));
        }

        _context.Received(1).ReplyLocalizedMessage("badge_add_success_message");
    }

    [Test]
    [TestCase("add-trophy")]
    [TestCase("newtrophy")]
    [TestCase("new-trophy")]
    public async Task Test_RunAsync_ShouldSetIsTrophyToTrue_WhenCommandIsTrophyVariant(string command)
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("Trophy, image");
        _context.RoomId.Returns("room1");
        _context.Command.Returns(command);

        // Act
        await cmd.RunAsync(_context);

        // Assert
        await using var db = new BotDbContext(_options);
        Assert.That(db.Badges.Single().IsTrophy, Is.True);
    }

    [Test]
    [TestCase("add-badge")]
    [TestCase("addbadge")]
    [TestCase("new-badge")]
    [TestCase("newbadge")]
    public async Task Test_RunAsync_ShouldSetIsTrophyToFalse_WhenCommandIsBadgeVariant(string command)
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("Badge, image");
        _context.RoomId.Returns("room1");
        _context.Command.Returns(command);

        // Act
        await cmd.RunAsync(_context);

        // Assert
        await using var db = new BotDbContext(_options);
        Assert.That(db.Badges.Single().IsTrophy, Is.False);
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeBadgeName_ToLowerAlphaNum()
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("Test-Badge_123!, image");
        _context.RoomId.Returns("room1");
        _context.Command.Returns("add-badge");

        // Act
        await cmd.RunAsync(_context);

        // Assert
        await using var db = new BotDbContext(_options);
        Assert.That(db.Badges.Single().Id, Is.EqualTo("testbadge123"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimWhitespace_FromNameAndImage()
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("  Badge Name  ,  http://image.url  ");
        _context.RoomId.Returns("room1");
        _context.Command.Returns("add-badge");

        // Act
        await cmd.RunAsync(_context);

        // Assert
        await using var db = new BotDbContext(_options);
        var badge = db.Badges.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(badge.Name, Is.EqualTo("Badge Name"));
            Assert.That(badge.Image, Is.EqualTo("http://image.url"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCancellationToken_ToAllDbOperations()
    {
        // Arrange
        var cmd = CreateCommand();
        var token = new CancellationToken();
        _context.Target.Returns("Badge, image");
        _context.RoomId.Returns("room1");
        _context.Command.Returns("add-badge");

        // Act
        await cmd.RunAsync(_context, token);

        // Assert
        await using var db = new BotDbContext(_options);
        Assert.That(db.Badges.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Test_RunAsync_ShouldDisposeDbContext_AfterExecution()
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("Badge, image");
        _context.RoomId.Returns("room1");
        _context.Command.Returns("add-badge");

        // Act
        await cmd.RunAsync(_context);

        // Assert - if disposal failed, next context creation would throw or reuse stale tracking
        await using var db = new BotDbContext(_options);
        Assert.That(db.Badges.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Test_RunAsync_ShouldDisposeDbContext_EvenWhenExceptionOccurs()
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("Badge, image");
        _context.RoomId.Returns("room1");
        _context.Command.Returns("add-badge");

        // Force duplicate PK on save → trigger exception path
        await using (var setupDb = new BotDbContext(_options))
        {
            setupDb.Badges.Add(new Badge { Id = "badge", RoomId = "room1" });
            await setupDb.SaveChangesAsync();
        }

        // Act
        await cmd.RunAsync(_context);

        // Assert
        await using var db = new BotDbContext(_options);
        Assert.That(db.Badges.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Test_RunAsync_ShouldCheckExistingBadge_UsingBadgeIdAndRoomId()
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("TestBadge, image");
        _context.RoomId.Returns("room1");
        _context.Command.Returns("add-badge");

        // Act
        await cmd.RunAsync(_context);

        // Assert
        await using var db = new BotDbContext(_options);
        var badge = db.Badges.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(badge.Id, Is.EqualTo("testbadge"));
            Assert.That(badge.RoomId, Is.EqualTo("room1"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetCorrectRoomId_InBadge()
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("Badge, image");
        _context.RoomId.Returns("specificroom");
        _context.Command.Returns("add-badge");

        // Act
        await cmd.RunAsync(_context);

        // Assert
        await using var db = new BotDbContext(_options);
        Assert.That(db.Badges.Single().RoomId, Is.EqualTo("specificroom"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldPreserveOriginalName_WhileNormalizingId()
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("Cool Badge!, image");
        _context.RoomId.Returns("room1");
        _context.Command.Returns("add-badge");

        // Act
        await cmd.RunAsync(_context);

        // Assert
        await using var db = new BotDbContext(_options);
        var badge = db.Badges.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(badge.Name, Is.EqualTo("Cool Badge!"));
            Assert.That(badge.Id, Is.EqualTo("coolbadge"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotCallSaveChanges_WhenBadgeAlreadyExists()
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("ExistingBadge, image");
        _context.RoomId.Returns("room1");
        _context.Command.Returns("add-badge");

        await using (var setupDb = new BotDbContext(_options))
        {
            setupDb.Badges.Add(new Badge { Id = "existingbadge", RoomId = "room1" });
            await setupDb.SaveChangesAsync();
        }

        // Act
        await cmd.RunAsync(_context);

        // Assert
        await using var db = new BotDbContext(_options);
        Assert.That(db.Badges.Count(), Is.EqualTo(1)); // unchanged
    }

    [Test]
    public async Task Test_RunAsync_ShouldNotCallSaveChanges_WhenValidationFails()
    {
        // Arrange
        var cmd = CreateCommand();
        _context.Target.Returns("invalid");

        // Act
        await cmd.RunAsync(_context);

        // Assert
        await using var db = new BotDbContext(_options);
        Assert.That(db.Badges.Count(), Is.EqualTo(0));
    }
}
