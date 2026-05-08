using System.Collections.Concurrent;
using ElsaMina.Core.Handlers.DefaultHandlers.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Core.Services.RoomUserData;

public class RoomUserDataService : IRoomUserDataService
{
    private const int TITLE_MAX_LENGTH = 450;
    private const int JOIN_PHRASE_MAX_LENGTH = 300;

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IUserSaveQueue _userSaveQueue;

    private readonly ConcurrentDictionary<Tuple<string, string>, string> _joinPhrases = new();

    public RoomUserDataService(IBotDbContextFactory dbContextFactory, IUserSaveQueue userSaveQueue)
    {
        _dbContextFactory = dbContextFactory;
        _userSaveQueue = userSaveQueue;
    }

    public IReadOnlyDictionary<Tuple<string, string>, string> JoinPhrases => _joinPhrases;

    public async Task InitializeJoinPhrasesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var usersWithJoinPhrase = await dbContext.RoomUsers
            .Where(userData => !string.IsNullOrEmpty(userData.JoinPhrase))
            .ToListAsync(cancellationToken);

        foreach (var userData in usersWithJoinPhrase)
        {
            _joinPhrases[Tuple.Create(userData.Id, userData.RoomId)] = userData.JoinPhrase!;
        }
    }

    public async Task<RoomUser> GetOrCreateRoomSpecificUserDataAsync(
        string roomId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var roomUser = await dbContext.RoomUsers
            .FirstOrDefaultAsync(roomUser => roomUser.Id == userId && roomUser.RoomId == roomId, cancellationToken);

        if (roomUser != null)
        {
            return roomUser;
        }

        var user = await dbContext.Users.FindAsync([userId], cancellationToken: cancellationToken);
        if (user == null)
        {
            // S'assurer que toutes les sauvegardes en attente sont finies avant de créer un nouvel user
            await _userSaveQueue.WaitForFlushAsync(cancellationToken);
            user = new SavedUser
            {
                UserId = userId,
                UserName = userId
            };
            await dbContext.Users.AddAsync(user, cancellationToken);
        }

        var newRoomUser = new RoomUser
        {
            Id = userId,
            RoomId = roomId,
            PlayTime = TimeSpan.Zero,
            User = user
        };

        await dbContext.RoomUsers.AddAsync(newRoomUser, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return newRoomUser;
    }

    public async Task GiveBadgeToUserAsync(
        string roomId,
        string userId,
        string badgeId,
        CancellationToken cancellationToken = default)
    {
        await GetOrCreateRoomSpecificUserDataAsync(roomId, userId, cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var badgeHolding = new BadgeHolding
        {
            BadgeId = badgeId,
            RoomId = roomId,
            UserId = userId
        };

        await dbContext.BadgeHoldings.AddAsync(badgeHolding, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task TakeBadgeFromUserAsync(
        string roomId,
        string userId,
        string badgeId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await dbContext.BadgeHoldings
            .FirstOrDefaultAsync(
                holding => holding.BadgeId == badgeId && holding.UserId == userId && holding.RoomId == roomId,
                cancellationToken);

        if (existing == null)
        {
            throw new ArgumentException("Badge not found");
        }

        dbContext.BadgeHoldings.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUserTitleAsync(
        string roomId,
        string userId,
        string title,
        CancellationToken cancellationToken = default)
    {
        if (title != null && title.Length > TITLE_MAX_LENGTH)
        {
            throw new ArgumentException("Title too long");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await GetOrCreateRoomSpecificUserDataAsync(roomId, userId, cancellationToken);

        user.Title = title;

        dbContext.RoomUsers.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUserEmojiAsync(
        string roomId,
        string userId,
        string emoji,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await GetOrCreateRoomSpecificUserDataAsync(roomId, userId, cancellationToken);

        user.ProfileEmoji = emoji;
        dbContext.RoomUsers.Update(user);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUserAvatarAsync(
        string roomId,
        string userId,
        string avatar,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(avatar) && !avatar.IsValidImageLink())
        {
            throw new ArgumentException("Invalid URL");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await GetOrCreateRoomSpecificUserDataAsync(roomId, userId, cancellationToken);

        user.Avatar = avatar;
        dbContext.RoomUsers.Update(user);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetUserJoinPhraseAsync(
        string roomId,
        string userId,
        string joinPhrase,
        CancellationToken cancellationToken = default)
    {
        if (joinPhrase != null && joinPhrase.Length > JOIN_PHRASE_MAX_LENGTH)
        {
            throw new ArgumentException("Join phrase too long");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await GetOrCreateRoomSpecificUserDataAsync(roomId, userId, cancellationToken);

        user.JoinPhrase = joinPhrase;

        var key = Tuple.Create(user.Id, user.RoomId);

        if (string.IsNullOrEmpty(joinPhrase))
        {
            _joinPhrases.Remove(key, out _);
        }
        else
        {
            _joinPhrases[key] = joinPhrase;
        }

        dbContext.RoomUsers.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}