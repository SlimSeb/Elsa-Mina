using ElsaMina.Commands.Dolls;
using ElsaMina.Commands.Profile;
using ElsaMina.Commands.Showdown.Ranking;
using ElsaMina.Core.Services.Formats;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Services.UserData;
using ElsaMina.Core.Services.UserDetails;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Profile;

public class ProfileServiceTest
{
    private IUserDetailsManager _userDetailsManager;
    private ITemplatesManager _templatesManager;
    private IUserDataService _userDataService;
    private IShowdownRanksProvider _showdownRanksProvider;
    private IFormatsManager _formatsManager;
    private IBotDbContextFactory _dbContextFactory;
    private IRoomsManager _roomsManager;
    private IDollService _dollService;
    private BotDbContext _dbContext;
    private ProfileService _sut;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new BotDbContext(options);

        _userDetailsManager = Substitute.For<IUserDetailsManager>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _userDataService = Substitute.For<IUserDataService>();
        _showdownRanksProvider = Substitute.For<IShowdownRanksProvider>();
        _formatsManager = Substitute.For<IFormatsManager>();
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _dollService = Substitute.For<IDollService>();

        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(_dbContext));
        _userDetailsManager.GetUserDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((UserDetailsDto)null);
        _userDataService.GetRegisterDateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.MinValue);
        _showdownRanksProvider.GetRankingDataAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IEnumerable<RankingDataDto>)null);
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("rendered");
        _dollService.ResolveDollsAsync(Arg.Any<IEnumerable<DollHolding>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        _sut = new ProfileService(
            _userDetailsManager,
            _templatesManager,
            _userDataService,
            _showdownRanksProvider,
            _formatsManager,
            _dbContextFactory,
            _roomsManager,
            _dollService);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    #region PlayTime

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetPlayTime_FromStoredUserData()
    {
        // Arrange
        var expectedPlayTime = TimeSpan.FromHours(3.5);
        _dbContext.RoomUsers.Add(new RoomUser
        {
            Id = "alice",
            RoomId = "room1",
            PlayTime = expectedPlayTime,
            User = new SavedUser { UserId = "alice", UserName = "Alice" }
        });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => vm.PlayTime == expectedPlayTime));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetPlayTimeToZero_WhenNoUserDataExists()
    {
        // Act
        await _sut.GetProfileHtmlAsync("unknown", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => vm.PlayTime == TimeSpan.Zero));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetProfileColorsAndEmoji_FromStoredUserData()
    {
        // Arrange
        _dbContext.RoomUsers.Add(new RoomUser
        {
            Id = "alice",
            RoomId = "room1",
            ProfileBackgroundColor = "#8867aa73",
            ProfileTextColor = "#e0d060",
            ProfileLabelColor = "#6ad0d0",
            ProfileEmoji = "🎮",
            Title = "Master",
            User = new SavedUser { UserId = "alice", UserName = "Alice" }
        });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm =>
                vm.ProfileBackgroundColor == "#8867aa73" &&
                vm.ProfileTextColor == "#e0d060" &&
                vm.ProfileLabelColor == "#6ad0d0" &&
                vm.ProfileEmoji == "🎮" &&
                vm.Title == "Master"));
    }

    #endregion

    #region UserName

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetUserName_FromRoomUser()
    {
        // Arrange
        _dbContext.RoomUsers.Add(new RoomUser
        {
            Id = "alice",
            RoomId = "room1",
            User = new SavedUser { UserId = "alice", UserName = "Alice" }
        });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => vm.UserName == "Alice"));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetUserName_FromSavedUser_WhenNoRoomUser()
    {
        // Arrange
        _dbContext.Users.Add(new SavedUser { UserId = "alice", UserName = "Alice" });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => vm.UserName == "Alice"));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldFallbackToUserId_WhenNoSavedUserExists()
    {
        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => vm.UserName == "alice"));
    }

    #endregion

    #region GameRecords - no RoomUser (fallback Users query path)

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetGameRecordsHasAnyRecordToFalse_WhenNoGameDataExists()
    {
        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => !vm.GameRecords.HasAnyRecord));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetFloodIt_WhenFloodItScoreExists_AndNoRoomUser()
    {
        // Arrange
        _dbContext.Users.Add(new SavedUser { UserId = "alice", UserName = "Alice" });
        _dbContext.FloodItScores.Add(new FloodItScore { UserId = "alice", Level = 7, BestMoves = 14, TotalStars = 3 });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm =>
                vm.GameRecords.FloodIt != null &&
                vm.GameRecords.FloodIt.Level == 7 &&
                vm.GameRecords.FloodIt.TotalStars == 3));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetLightsOut_WhenLightsOutScoreExists_AndNoRoomUser()
    {
        // Arrange
        _dbContext.Users.Add(new SavedUser { UserId = "alice", UserName = "Alice" });
        _dbContext.LightsOutScores.Add(new LightsOutScore { UserId = "alice", Level = 9, BestMoves = 8, TotalStars = 2 });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm =>
                vm.GameRecords.LightsOut != null &&
                vm.GameRecords.LightsOut.Level == 9 &&
                vm.GameRecords.LightsOut.TotalStars == 2));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetVoltorbFlip_WhenVoltorbFlipLevelExists_AndNoRoomUser()
    {
        // Arrange
        _dbContext.Users.Add(new SavedUser { UserId = "alice", UserName = "Alice" });
        _dbContext.VoltorbFlipLevels.Add(new VoltorbFlipLevel { UserId = "alice", Level = 3, MaxLevel = 6, Coins = 1500 });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm =>
                vm.GameRecords.VoltorbFlip != null &&
                vm.GameRecords.VoltorbFlip.MaxLevel == 6 &&
                vm.GameRecords.VoltorbFlip.Coins == 1500));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetTwentyFortyEight_WhenScoreExists_AndNoRoomUser()
    {
        // Arrange
        _dbContext.Users.Add(new SavedUser { UserId = "alice", UserName = "Alice" });
        _dbContext.TwentyFortyEightScores.Add(new TwentyFortyEightScore { UserId = "alice", BestScore = 8192, Wins = 4 });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm =>
                vm.GameRecords.TwentyFortyEight != null &&
                vm.GameRecords.TwentyFortyEight.BestScore == 8192 &&
                vm.GameRecords.TwentyFortyEight.Wins == 4));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetHasAnyRecordToTrue_WhenAtLeastOneGameRecordExists_AndNoRoomUser()
    {
        // Arrange
        _dbContext.Users.Add(new SavedUser { UserId = "alice", UserName = "Alice" });
        _dbContext.TwentyFortyEightScores.Add(new TwentyFortyEightScore { UserId = "alice", BestScore = 1024, Wins = 1 });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => vm.GameRecords.HasAnyRecord));
    }

    #endregion

    #region GameRecords - with RoomUser

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetFloodIt_WhenFloodItScoreExists_AndRoomUserPresent()
    {
        // Arrange
        _dbContext.RoomUsers.Add(new RoomUser
        {
            Id = "alice",
            RoomId = "room1",
            User = new SavedUser { UserId = "alice", UserName = "Alice" }
        });
        _dbContext.FloodItScores.Add(new FloodItScore { UserId = "alice", Level = 7, BestMoves = 14, TotalStars = 3 });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm =>
                vm.GameRecords.FloodIt != null &&
                vm.GameRecords.FloodIt.Level == 7 &&
                vm.GameRecords.FloodIt.TotalStars == 3));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetLightsOut_WhenLightsOutScoreExists_AndRoomUserPresent()
    {
        // Arrange
        _dbContext.RoomUsers.Add(new RoomUser
        {
            Id = "alice",
            RoomId = "room1",
            User = new SavedUser { UserId = "alice", UserName = "Alice" }
        });
        _dbContext.LightsOutScores.Add(new LightsOutScore { UserId = "alice", Level = 9, BestMoves = 8, TotalStars = 2 });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm =>
                vm.GameRecords.LightsOut != null &&
                vm.GameRecords.LightsOut.Level == 9 &&
                vm.GameRecords.LightsOut.TotalStars == 2));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetVoltorbFlip_WhenVoltorbFlipLevelExists_AndRoomUserPresent()
    {
        // Arrange
        _dbContext.RoomUsers.Add(new RoomUser
        {
            Id = "alice",
            RoomId = "room1",
            User = new SavedUser { UserId = "alice", UserName = "Alice" }
        });
        _dbContext.VoltorbFlipLevels.Add(new VoltorbFlipLevel { UserId = "alice", Level = 3, MaxLevel = 6, Coins = 1500 });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm =>
                vm.GameRecords.VoltorbFlip != null &&
                vm.GameRecords.VoltorbFlip.MaxLevel == 6 &&
                vm.GameRecords.VoltorbFlip.Coins == 1500));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetTwentyFortyEight_WhenScoreExists_AndRoomUserPresent()
    {
        // Arrange
        _dbContext.RoomUsers.Add(new RoomUser
        {
            Id = "alice",
            RoomId = "room1",
            User = new SavedUser { UserId = "alice", UserName = "Alice" }
        });
        _dbContext.TwentyFortyEightScores.Add(new TwentyFortyEightScore { UserId = "alice", BestScore = 8192, Wins = 4 });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm =>
                vm.GameRecords.TwentyFortyEight != null &&
                vm.GameRecords.TwentyFortyEight.BestScore == 8192 &&
                vm.GameRecords.TwentyFortyEight.Wins == 4));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetAllGameRecordsToNull_WhenRoomUserExistsWithNoGameData()
    {
        // Arrange
        _dbContext.RoomUsers.Add(new RoomUser
        {
            Id = "alice",
            RoomId = "room1",
            User = new SavedUser { UserId = "alice", UserName = "Alice" }
        });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => !vm.GameRecords.HasAnyRecord));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetConnectFour_WhenRatingExists()
    {
        // Arrange
        _dbContext.Users.Add(new SavedUser { UserId = "alice", UserName = "Alice" });
        _dbContext.ConnectFourRatings.Add(new ConnectFourRating { UserId = "alice", Rating = 1150, Wins = 8, Losses = 3, Draws = 1 });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm =>
                vm.GameRecords.ConnectFour != null &&
                vm.GameRecords.ConnectFour.Rating == 1150 &&
                vm.GameRecords.ConnectFour.Wins == 8 &&
                vm.GameRecords.ConnectFour.Losses == 3 &&
                vm.GameRecords.ConnectFour.Draws == 1));
    }

    #endregion

    #region GameRecords - isolation

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldNotReturnOtherUsersGameRecords()
    {
        // Arrange
        _dbContext.Users.Add(new SavedUser { UserId = "bob", UserName = "Bob" });
        _dbContext.FloodItScores.Add(new FloodItScore { UserId = "bob", Level = 10, BestMoves = 5, TotalStars = 3 });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => vm.GameRecords.FloodIt == null));
    }

    #endregion

    #region Dolls

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetDolls_ResolvedFromTheOwnedHoldings()
    {
        // Arrange
        await AddDollHoldingsAsync("room1", "alice", "pikachu", "snorlax");
        var resolvedDolls = new List<Doll>
        {
            new() { Id = "snorlax", Name = "Snorlax", Size = 32, Image = "https://images/snorlax.png" },
            new() { Id = "pikachu", Name = "Pikachu", Size = 16, Image = "https://images/pikachu.png" }
        };
        _dollService.ResolveDollsAsync(Arg.Any<IEnumerable<DollHolding>>(), Arg.Any<CancellationToken>())
            .Returns(resolvedDolls);

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _dollService.Received(1).ResolveDollsAsync(
            Arg.Is<IEnumerable<DollHolding>>(holdings => holdings.Select(holding => holding.DollId)
                .OrderBy(dollId => dollId)
                .SequenceEqual(new[] { "pikachu", "snorlax" })),
            Arg.Any<CancellationToken>());
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(viewModel => viewModel.Dolls == resolvedDolls));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldNotResolveDollsOwnedInAnotherRoom()
    {
        // Arrange
        await AddDollHoldingsAsync("room2", "alice", "pikachu");

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _dollService.Received(1).ResolveDollsAsync(
            Arg.Is<IEnumerable<DollHolding>>(holdings => !holdings.Any()),
            Arg.Any<CancellationToken>());
    }

    private async Task AddDollHoldingsAsync(string roomId, string userId, params string[] dollIds)
    {
        _dbContext.RoomUsers.Add(new RoomUser
        {
            Id = userId,
            RoomId = roomId,
            User = new SavedUser { UserId = userId, UserName = userId },
            Dolls = dollIds.Select(dollId => new DollHolding
            {
                DollId = dollId,
                RoomId = roomId,
                UserId = userId
            }).ToList()
        });
        await _dbContext.SaveChangesAsync();
    }

    #endregion

    #region Online / LastSeen

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetIsOnlineToTrue_WhenUserDetailsHasRooms()
    {
        // Arrange
        _userDetailsManager.GetUserDetailsAsync("alice", Arg.Any<CancellationToken>())
            .Returns(new UserDetailsDto { Rooms = new Dictionary<string, UserDetailsRoomDto>() });

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => vm.IsOnline));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetIsOnlineToFalse_WhenUserDetailsIsNull()
    {
        // Arrange
        _userDetailsManager.GetUserDetailsAsync("alice", Arg.Any<CancellationToken>())
            .Returns((UserDetailsDto)null);

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => !vm.IsOnline));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetIsOnlineToFalse_WhenUserDetailsHasNullRooms()
    {
        // Arrange
        _userDetailsManager.GetUserDetailsAsync("alice", Arg.Any<CancellationToken>())
            .Returns(new UserDetailsDto { Rooms = null });

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => !vm.IsOnline));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetLastSeenDate_WhenSavedUserHasLastOnline()
    {
        // Arrange
        var lastOnline = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        _dbContext.Users.Add(new SavedUser
        {
            UserId = "alice",
            UserName = "Alice",
            LastOnline = lastOnline,
            LastSeenAction = UserAction.Chatting,
            LastSeenRoomId = "room1"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => vm.LastSeenDate.HasValue));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetLastSeenDateToNull_WhenNoSavedUserExists()
    {
        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => !vm.LastSeenDate.HasValue));
    }

    [Test]
    public async Task Test_GetProfileHtmlAsync_ShouldSetLastSeenAction_WhenSavedUserHasLastSeenAction()
    {
        // Arrange
        _dbContext.Users.Add(new SavedUser
        {
            UserId = "alice",
            UserName = "Alice",
            LastOnline = DateTimeOffset.UtcNow,
            LastSeenAction = UserAction.Leaving,
            LastSeenRoomId = "room1"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        await _sut.GetProfileHtmlAsync("alice", "room1");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Profile/Profile",
            Arg.Is<ProfileViewModel>(vm => vm.LastSeenAction == UserAction.Leaving));
    }

    #endregion

    #region GetAvatar

    [Test]
    public void Test_GetAvatar_ShouldReturnDefaultAvatar_WhenNoStoredOrShowdownAvatar()
    {
        var avatar = ProfileService.GetAvatar(null, null);

        Assert.That(avatar, Is.EqualTo("https://play.pokemonshowdown.com/sprites/trainers/unknown.png"));
    }

    [Test]
    public void Test_GetAvatar_ShouldReturnCustomStoredAvatar_WhenPresent()
    {
        var stored = new RoomUser { Avatar = "https://custom/avatar.png" };

        var avatar = ProfileService.GetAvatar(stored, null);

        Assert.That(avatar, Is.EqualTo("https://custom/avatar.png"));
    }

    [Test]
    public void Test_GetAvatar_ShouldReturnCustomUrl_WhenAvatarStartsWithHash()
    {
        var details = new UserDetailsDto { Avatar = "#123" };

        var avatar = ProfileService.GetAvatar(null, details);

        Assert.That(avatar.Contains("trainers-custom/123.png"), Is.True);
    }

    #endregion
}
