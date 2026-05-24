using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ElsaMina.Core.Handlers.DefaultHandlers.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.RoomUserData;

public class RoomUserDataServiceTest
{
    private BotDbContext _db;
    private RoomUserDataService _service;
    private IBotDbContextFactory _dbContextFactory;
    private DbContextOptions<BotDbContext> _options;
    private IUserSaveQueue _userSaveQueue;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        // Shared context used ONLY for validation in tests.
        _db = new BotDbContext(_options);

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();

        // IMPORTANT: return a NEW CONTEXT every time the service asks for one.
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var freshContext = new BotDbContext(_options);
                return Task.FromResult(freshContext);
            });

        _userSaveQueue = Substitute.For<IUserSaveQueue>();
        _service = new RoomUserDataService(_dbContextFactory, _userSaveQueue);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public async Task Test_GetUserData_ShouldReturnUserData_IfExists()
    {
        // Arrange
        var roomId = "room1";
        var userId = "user1";
        var existingUserData = new RoomUser { Id = userId, RoomId = roomId };
        await _db.RoomUsers.AddAsync(existingUserData);
        await _db.SaveChangesAsync();

        // Act
        var result = await _service.GetOrCreateRoomSpecificUserDataAsync(roomId, userId);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Id, Is.EqualTo(userId));
            Assert.That(result.RoomId, Is.EqualTo(roomId));
        }
    }

    [Test]
    public async Task Test_GetUserData_ShouldCreateUserData_IfNotExists()
    {
        // Arrange
        var roomId = "room1";
        var userId = "user1";

        // Act
        var result = await _service.GetOrCreateRoomSpecificUserDataAsync(roomId, userId);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Id, Is.EqualTo(userId));
            Assert.That(result.RoomId, Is.EqualTo(roomId));
        }

        var dbUser = await _db.RoomUsers.FindAsync(userId, roomId);
        Assert.That(dbUser, Is.Not.Null);
    }

    [Test]
    public async Task Test_InitializeJoinPhrases_ShouldPopulateJoinPhrasesDictionary()
    {
        // Arrange
        var userDataList = new List<RoomUser>
        {
            new RoomUser { Id = "user1", RoomId = "room1", JoinPhrase = "Hello" },
            new RoomUser { Id = "user2", RoomId = "room2", JoinPhrase = "Welcome" }
        };
        await _db.RoomUsers.AddRangeAsync(userDataList);
        await _db.SaveChangesAsync();

        // Act
        await _service.InitializeJoinPhrasesAsync();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_service.JoinPhrases.Count, Is.EqualTo(2));
            Assert.That(_service.JoinPhrases[Tuple.Create("user1", "room1")], Is.EqualTo("Hello"));
            Assert.That(_service.JoinPhrases[Tuple.Create("user2", "room2")], Is.EqualTo("Welcome"));
        }
    }

    [Test]
    public async Task Test_GiveBadgeToUser_ShouldAddBadge()
    {
        // Arrange
        var roomId = "room1";
        var userId = "user1";
        var badgeId = "badge1";

        // Act
        await _service.GiveBadgeToUserAsync(roomId, userId, badgeId);

        // Assert
        var badge = await _db.BadgeHoldings.FindAsync(badgeId, userId, roomId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(badge, Is.Not.Null);
            Assert.That(badge.BadgeId, Is.EqualTo(badgeId));
        }
    }

    [Test]
    public void Test_TakeBadgeFromUser_ShouldThrowArgumentException_WhenBadgeNotFound()
    {
        // Arrange
        var roomId = "room1";
        var userId = "user1";
        var badgeId = "badge1";

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => _service.TakeBadgeFromUserAsync(roomId, userId, badgeId));
    }

    [Test]
    public async Task Test_TakeBadgeFromUser_ShouldDeleteBadge_WhenFound()
    {
        // Arrange
        var roomId = "room1";
        var userId = "user1";
        var badgeId = "badge1";
        var badgeHolding = new BadgeHolding { BadgeId = badgeId, RoomId = roomId, UserId = userId };
        await _db.BadgeHoldings.AddAsync(badgeHolding);
        await _db.SaveChangesAsync();

        // Act
        await _service.TakeBadgeFromUserAsync(roomId, userId, badgeId);

        // Assert
        var dbBadge = await _db.BadgeHoldings.FindAsync(badgeId, roomId, userId);
        Assert.That(dbBadge, Is.Null);
    }

    [Test]
    public void Test_SetUserTitle_ShouldThrowArgumentException_WhenTitleTooLong()
    {
        // Arrange
        var roomId = "room1";
        var userId = "user1";
        var title = new string('a', 451);

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => _service.SetUserTitleAsync(roomId, userId, title));
    }

    [Test]
    public async Task Test_SetUserTitle_ShouldUpdateUserDataTitle_WhenValid()
    {
        // Arrange
        var roomId = "room1";
        var userId = "user1";
        var title = "Valid Title";
        var userData = new RoomUser { Id = userId, RoomId = roomId };
        await _db.RoomUsers.AddAsync(userData);
        await _db.SaveChangesAsync();

        // Act
        await _service.SetUserTitleAsync(roomId, userId, title);

        // Assert
        await using var dbContext = new BotDbContext(_options);
        var dbUser = await dbContext.RoomUsers.FindAsync(userId, roomId);
        Assert.That(dbUser.Title, Is.EqualTo(title));
    }

    [Test]
    public void Test_SetUserAvatar_ShouldThrowArgumentException_WhenInvalidUrl()
    {
        // Arrange
        var roomId = "room1";
        var userId = "user1";
        var invalidAvatar = "invalid_url";

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => _service.SetUserAvatarAsync(roomId, userId, invalidAvatar));
    }

    [Test]
    public async Task Test_SetUserAvatar_ShouldUpdateUserDataAvatar_WhenValid()
    {
        // Arrange
        var roomId = "room1";
        var userId = "user1";
        var avatar = "https://valid.url/image.jpg";
        var userData = new RoomUser { Id = userId, RoomId = roomId };
        await _db.RoomUsers.AddAsync(userData);
        await _db.SaveChangesAsync();

        // Act
        await _service.SetUserAvatarAsync(roomId, userId, avatar);

        // Assert
        await using var dbContext = new BotDbContext(_options);
        var dbUser = await dbContext.RoomUsers.FindAsync(userId, roomId);
        Assert.That(dbUser.Avatar, Is.EqualTo(avatar));
    }

    [Test]
    public void Test_SetUserJoinPhrase_ShouldThrowArgumentException_WhenJoinPhraseTooLong()
    {
        // Arrange
        var roomId = "room1";
        var userId = "user1";
        var joinPhrase = new string('a', 301);

        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(() => _service.SetUserJoinPhraseAsync(roomId, userId, joinPhrase));
    }

    [Test]
    public async Task Test_SetUserJoinPhrase_ShouldUpdateJoinPhrase_WhenValid()
    {
        // Arrange
        var roomId = "room1";
        var userId = "user1";
        var joinPhrase = "Welcome!";
        var userData = new RoomUser { Id = userId, RoomId = roomId };
        await _db.RoomUsers.AddAsync(userData);
        await _db.SaveChangesAsync();

        // Act
        await _service.SetUserJoinPhraseAsync(roomId, userId, joinPhrase);

        // Assert
        await using var dbContext = new BotDbContext(_options);
        var dbUser = await dbContext.RoomUsers.FindAsync(userId, roomId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dbUser.JoinPhrase, Is.EqualTo(joinPhrase));
            Assert.That(_service.JoinPhrases[Tuple.Create(userId, roomId)], Is.EqualTo(joinPhrase));
        }
    }
}