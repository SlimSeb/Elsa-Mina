using ElsaMina.Commands.Badges.BadgeHolders;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NUnit.Framework;

namespace ElsaMina.UnitTests.Commands.Badges.BadgeHolders;

[TestFixture]
public class BadgeHoldersCommandTest
{
    private IContext _context;
    private ITemplatesManager _templatesManager;
    private IBotDbContextFactory _dbContextFactory;
    private BadgeHoldersCommand _command;

    private static DbContextOptions<BotDbContext> CreateNewInMemoryOptions() =>
        new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _command = new BadgeHoldersCommand(_templatesManager, _dbContextFactory);
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
    public async Task Test_RunAsync_ShouldDisplayBadges_WhenBadgesExist()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using (var seedCtx = new BotDbContext(options))
        {
            await seedCtx.Database.EnsureCreatedAsync();

            var savedUser = new SavedUser { UserId = "user1", UserName = "User One" };
            var roomUser = new RoomUser { Id = "user1", RoomId = "room1", User = savedUser };
            var badge1 = new Badge { Id = "badge1", Name = "Badge A", RoomId = "room1", IsTrophy = false };
            var badge2 = new Badge { Id = "badge2", Name = "Badge B", RoomId = "room1", IsTrophy = false };
            badge1.BadgeHolders.Add(new BadgeHolding { UserId = "user1", BadgeId = "badge1", RoomId = "room1", RoomUser = roomUser });

            await seedCtx.Users.AddAsync(savedUser);
            await seedCtx.RoomUsers.AddAsync(roomUser);
            await seedCtx.Badges.AddRangeAsync(badge1, badge2);
            await seedCtx.SaveChangesAsync();
        }

        await using var queryCtx = new BotDbContext(options);
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(queryCtx);

        _context.RoomId.Returns("room1");
        _context.Culture.Returns(System.Globalization.CultureInfo.InvariantCulture);

        var expectedHtml = "<div>Badge Holders</div>";
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns(expectedHtml);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Badges/BadgeHolders/BadgeHolders", Arg.Is<BadgeHoldersViewModel>(vm =>
            vm.Badges.Length == 2 &&
            vm.Badges[0].Id == "badge1" &&
            vm.Badges[0].BadgeHolders.Count == 1 &&
            vm.Badges[1].Id == "badge2"
        ));

        _context.Received(1).ReplyHtml(expectedHtml, null, true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldFilterByRoom_AndSortByName()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using (var seedCtx = new BotDbContext(options))
        {
            await seedCtx.Database.EnsureCreatedAsync();
            await seedCtx.Badges.AddRangeAsync(
                new Badge { Id = "b1", Name = "Zebra Badge", RoomId = "room1" },
                new Badge { Id = "b2", Name = "Apple Badge", RoomId = "room1" },
                new Badge { Id = "b3", Name = "Other Room Badge", RoomId = "other_room" }
            );
            await seedCtx.SaveChangesAsync();
        }

        await using var queryCtx = new BotDbContext(options);
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(queryCtx);

        _context.RoomId.Returns("room1");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("html");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(Arg.Any<string>(), Arg.Is<BadgeHoldersViewModel>(vm =>
            vm.Badges.Length == 2 &&
            vm.Badges[0].Name == "Apple Badge" &&
            vm.Badges[1].Name == "Zebra Badge"
        ));
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeRoomUserWithUser_WhenBadgeHoldingHasRoomUser()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using (var seedCtx = new BotDbContext(options))
        {
            await seedCtx.Database.EnsureCreatedAsync();

            var savedUser = new SavedUser { UserId = "user1", UserName = "Test User" };
            var roomUser = new RoomUser { Id = "user1", RoomId = "room1", User = savedUser };
            var badge = new Badge { Id = "badge1", Name = "Badge A", RoomId = "room1" };
            badge.BadgeHolders.Add(new BadgeHolding { UserId = "user1", BadgeId = "badge1", RoomId = "room1", RoomUser = roomUser });

            await seedCtx.Users.AddAsync(savedUser);
            await seedCtx.RoomUsers.AddAsync(roomUser);
            await seedCtx.Badges.AddAsync(badge);
            await seedCtx.SaveChangesAsync();
        }

        await using var queryCtx = new BotDbContext(options);
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(queryCtx);

        _context.RoomId.Returns("room1");
        _context.Culture.Returns(System.Globalization.CultureInfo.InvariantCulture);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("html");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(Arg.Any<string>(), Arg.Is<BadgeHoldersViewModel>(vm =>
            vm.Badges.Length == 1 &&
            vm.Badges[0].BadgeHolders.Count == 1 &&
            vm.Badges[0].BadgeHolders.First().RoomUser != null &&
            vm.Badges[0].BadgeHolders.First().RoomUser.User != null &&
            vm.Badges[0].BadgeHolders.First().RoomUser.User.UserName == "Test User"
        ));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTargetAsRoomId_WhenTargetIsProvided()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using (var seedCtx = new BotDbContext(options))
        {
            await seedCtx.Database.EnsureCreatedAsync();
            await seedCtx.Badges.AddRangeAsync(
                new Badge { Id = "b1", Name = "Target Badge", RoomId = "targetroom" },
                new Badge { Id = "b2", Name = "Current Badge", RoomId = "currentroom" }
            );
            await seedCtx.SaveChangesAsync();
        }

        await using var queryCtx = new BotDbContext(options);
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(queryCtx);

        _context.Target.Returns("targetroom");
        _context.RoomId.Returns("currentroom");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("html");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(Arg.Any<string>(), Arg.Is<BadgeHoldersViewModel>(vm =>
            vm.Badges.Length == 1 &&
            vm.Badges[0].RoomId == "targetroom"
        ));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseContextRoomId_WhenTargetIsEmpty()
    {
        // Arrange
        var options = CreateNewInMemoryOptions();
        await using (var seedCtx = new BotDbContext(options))
        {
            await seedCtx.Database.EnsureCreatedAsync();
            await seedCtx.Badges.AddRangeAsync(
                new Badge { Id = "b1", Name = "Current Badge", RoomId = "currentroom" },
                new Badge { Id = "b2", Name = "Other Badge", RoomId = "otherroom" }
            );
            await seedCtx.SaveChangesAsync();
        }

        await using var queryCtx = new BotDbContext(options);
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(queryCtx);

        _context.Target.Returns(string.Empty);
        _context.RoomId.Returns("currentroom");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("html");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(Arg.Any<string>(), Arg.Is<BadgeHoldersViewModel>(vm =>
            vm.Badges.Length == 1 &&
            vm.Badges[0].RoomId == "currentroom"
        ));
    }
}
