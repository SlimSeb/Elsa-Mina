using ElsaMina.Commands.Profile.EditProfilePanel;
using ElsaMina.Commands.Showdown.Ranking;
using ElsaMina.Core.Services.Formats;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Services.UserData;
using ElsaMina.Core.Services.UserDetails;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Profile;

public class ProfileService : IProfileService
{
    private const string DEFAULT_AVATAR_ID = "167";
    private const string AVATAR_URL = "https://play.pokemonshowdown.com/sprites/trainers/{0}.png";
    private const string AVATAR_CUSTOM_URL = "https://play.pokemonshowdown.com/sprites/trainers-custom/{0}.png";

    private readonly IUserDetailsManager _userDetailsManager;
    private readonly ITemplatesManager _templatesManager;
    private readonly IUserDataService _userDataService;
    private readonly IShowdownRanksProvider _showdownRanksProvider;
    private readonly IFormatsManager _formatsManager;
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IRoomsManager _roomsManager;

    public ProfileService(IUserDetailsManager userDetailsManager,
        ITemplatesManager templatesManager,
        IUserDataService userDataService,
        IShowdownRanksProvider showdownRanksProvider,
        IFormatsManager formatsManager,
        IBotDbContextFactory dbContextFactory,
        IRoomsManager roomsManager)
    {
        _userDetailsManager = userDetailsManager;
        _templatesManager = templatesManager;
        _userDataService = userDataService;
        _showdownRanksProvider = showdownRanksProvider;
        _formatsManager = formatsManager;
        _dbContextFactory = dbContextFactory;
        _roomsManager = roomsManager;
    }

    public async Task<string> GetProfileHtmlAsync(string userId, string roomId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var userDetailsTask = _userDetailsManager.GetUserDetailsAsync(userId, cancellationToken);
        var registerDateTask = _userDataService.GetRegisterDateAsync(userId, cancellationToken);
        var ranksTask = _showdownRanksProvider.GetRankingDataAsync(userId, cancellationToken);

        // Foreign keys guarantee that any user-owned row (room data, game scores) points at a Users row,
        // so the whole profile can be loaded from a single Users query through the navigation properties.
        var savedUser = await dbContext.Users
            .AsSplitQuery()
            .Include(user => user.RoomData.Where(roomUser => roomUser.RoomId == roomId))
            .ThenInclude(roomUser => roomUser.Badges)
            .ThenInclude(badgeHolding => badgeHolding.Badge)
            .Include(user => user.RoomData.Where(roomUser => roomUser.RoomId == roomId))
            .ThenInclude(roomUser => roomUser.TournamentRecord)
            .Include(user => user.FloodItScore)
            .Include(user => user.LightsOutScore)
            .Include(user => user.VoltorbFlipLevel)
            .Include(user => user.TwentyFortyEightScore)
            .Include(user => user.ConnectFourRating)
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserId == userId, cancellationToken);

        var storedUserData = savedUser?.RoomData.FirstOrDefault();

        await Task.WhenAll(userDetailsTask, registerDateTask, ranksTask);

        var showdownUserDetails = userDetailsTask.Result;
        var registerDate = registerDateTask.Result;

        var room = _roomsManager.GetRoom(roomId);
        var culture = room?.Culture;
        var avatarUrl = GetAvatar(storedUserData, showdownUserDetails);
        var bestRanking = ranksTask.Result?.MaxBy(ranking => ranking.Elo);
        bestRanking?.FormatId = _formatsManager.GetCleanFormat(bestRanking.FormatId);

        var userRoomRank = GetUserRoomRank(roomId, showdownUserDetails);
        var userName = showdownUserDetails?.Name != userId && !string.IsNullOrEmpty(showdownUserDetails?.Name)
            ? showdownUserDetails.Name
            : savedUser?.UserName ?? userId;

        var gameRecords = new GameRecords
        {
            FloodIt = savedUser?.FloodItScore,
            LightsOut = savedUser?.LightsOutScore,
            VoltorbFlip = savedUser?.VoltorbFlipLevel,
            TwentyFortyEight = savedUser?.TwentyFortyEightScore,
            ConnectFour = savedUser?.ConnectFourRating
        };

        var isOnline = showdownUserDetails?.Rooms != null;
        var lastSeenDate = savedUser?.LastOnline.HasValue == true
            ? TimeZoneInfo.ConvertTime(savedUser.LastOnline.Value, room?.TimeZone ?? TimeZoneInfo.Local)
            : (DateTimeOffset?)null;

        var viewModel = new ProfileViewModel
        {
            Culture = culture,
            Avatar = avatarUrl,
            UserId = userId,
            UserName = userName,
            UserRoomRank = userRoomRank,
            Status = GetStatus(showdownUserDetails),
            Badges = storedUserData?.Badges.Select(holding => holding.Badge),
            Title = storedUserData?.Title,
            ProfileEmoji = storedUserData?.ProfileEmoji,
            ProfileBackgroundColor = storedUserData?.ProfileBackgroundColor,
            ProfileBorderColor = EditProfilePanelCommand.GetBorderColor(storedUserData?.ProfileBackgroundColor),
            RegisterDate = TimeZoneInfo.ConvertTime(registerDate, room?.TimeZone ?? TimeZoneInfo.Local),
            BestRanking = bestRanking,
            TournamentRecord = storedUserData?.TournamentRecord,
            PlayTime = storedUserData?.PlayTime ?? TimeSpan.Zero,
            GameRecords = gameRecords,
            IsOnline = isOnline,
            LastSeenDate = lastSeenDate,
            LastSeenAction = savedUser?.LastSeenAction
        };

        return await _templatesManager.GetTemplateAsync("Profile/Profile", viewModel);
    }

    private static char GetUserRoomRank(string roomId, UserDetailsDto showdownUserDetails)
    {
        var userRoom = showdownUserDetails?
            .Rooms?
            .Keys
            .FirstOrDefault(roomName => roomName.ToLowerAlphaNum() == roomId);
        return userRoom != null ? userRoom[0] : ' ';
    }

    private static string GetStatus(UserDetailsDto showdownUserDetails)
    {
        var status = showdownUserDetails?.Status;
        if (status?.StartsWith('!') == true)
        {
            status = status[1..];
        }

        return status;
    }

    public static string GetAvatar(RoomUser storedRoomUserData, UserDetailsDto showdownUserDetails)
    {
        string avatarUrl;
        if (!string.IsNullOrEmpty(storedRoomUserData?.Avatar))
        {
            avatarUrl = storedRoomUserData.Avatar;
        }
        else
        {
            var avatarId = showdownUserDetails?.Avatar ?? DEFAULT_AVATAR_ID;
            if (BattleAvatars.AVATAR_NUMBERS.TryGetValue(avatarId, out var avatarName))
            {
                avatarId = avatarName;
            }

            var avatarBaseUrl = AVATAR_URL;
            if (avatarId.StartsWith('#'))
            {
                avatarId = avatarId[1..];
                avatarBaseUrl = AVATAR_CUSTOM_URL;
            }

            avatarUrl = string.Format(avatarBaseUrl, avatarId);
        }

        return avatarUrl;
    }
}