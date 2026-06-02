using ElsaMina.Commands.Profile;
using ElsaMina.Core;
using ElsaMina.Core.Handlers;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Tournaments.Handlers;

public class TourEndHandler : Handler
{
    private const int PRIZE_PER_PARTICIPANT = 2;

    private readonly IBotDbContextFactory _botDbContextFactory;
    private readonly IRoomUserDataService _roomUserDataService;
    private readonly IProfileService _profileService;
    private readonly IRoomsManager _roomsManager;
    private readonly IResourcesService _resourcesService;
    private readonly IBot _bot;
    private readonly IClockService _clockService;

    public TourEndHandler(IBotDbContextFactory botDbContextFactory,
        IRoomUserDataService roomUserDataService,
        IProfileService profileService,
        IRoomsManager roomsManager,
        IResourcesService resourcesService,
        IBot bot,
        IClockService clockService)
    {
        _botDbContextFactory = botDbContextFactory;
        _roomUserDataService = roomUserDataService;
        _profileService = profileService;
        _roomsManager = roomsManager;
        _resourcesService = resourcesService;
        _bot = bot;
        _clockService = clockService;
    }

    public override IReadOnlySet<string> HandledMessageTypes { get; } = new HashSet<string> { "tournament" };

    public override async Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        if (parts.Length < 4 || parts[1] != "tournament" || parts[2] != "end")
        {
            return;
        }

        var result = TournamentHelper.ParseTourResults(parts[3]);
        if (result == null)
        {
            return;
        }

        var room = _roomsManager.GetRoom(roomId);
        var isBucksEnabled = room != null &&
                             (await room.GetParameterValueAsync(Parameter.BucksEnabled, cancellationToken)).ToBoolean();

        try
        {
            await using var dbContext = await _botDbContextFactory.CreateDbContextAsync(cancellationToken);

            var savedTournament = new SavedTournament
            {
                RoomId = roomId,
                Format = result.Format,
                Winner = result.Winner,
                RunnerUp = result.RunnerUp,
                SemiFinalists = string.Join(",", result.SemiFinalists),
                PlayerCount = result.Players.Count,
                EndedAt = _clockService.CurrentUtcDateTimeOffset
            };
            await dbContext.SavedTournaments.AddAsync(savedTournament, cancellationToken);

            foreach (var player in result.Players)
            {
                var userId = player.ToLowerAlphaNum();
                await _roomUserDataService.GetOrCreateRoomSpecificUserDataAsync(roomId, userId, cancellationToken);

                var wonGamesInTournament = result.WinsCount.GetValueOrDefault(userId, 0);
                var playedGamesInTournament = wonGamesInTournament + (userId == result.Winner ? 0 : 1);

                var record = await dbContext.TournamentRecords.FindAsync([userId, roomId], cancellationToken);
                if (record == null)
                {
                    record = new TournamentRecord { UserId = userId, RoomId = roomId };
                    await dbContext.TournamentRecords.AddAsync(record, cancellationToken);
                }

                record.TournamentsEnteredCount++;
                record.WinsCount += userId == result.Winner ? 1 : 0;
                record.RunnerUpCount += userId == result.RunnerUp ? 1 : 0;
                record.ThirdPlaceCount += result.SemiFinalists.Contains(userId) ? 1 : 0;
                record.WonGames += wonGamesInTournament;
                record.PlayedGames += playedGamesInTournament;
            }

            if (isBucksEnabled && !string.IsNullOrEmpty(result.Winner) && result.Players.Count > 0)
            {
                var prize = result.Players.Count * PRIZE_PER_PARTICIPANT;
                // The winner is one of the players, so their room data was created in the loop above.
                var winnerId = result.Winner.ToLowerAlphaNum();
                var winnerRoomUser = await dbContext.RoomUsers.FindAsync([winnerId, roomId], cancellationToken);
                if (winnerRoomUser != null)
                {
                    winnerRoomUser.Money += prize;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving tournament results for room {RoomId}", roomId);
        }

        if (result.Winner == null)
        {
            return;
        }

        if (isBucksEnabled && result.Players.Count > 0)
        {
            var prize = result.Players.Count * PRIZE_PER_PARTICIPANT;
            var prizeMessage = string.Format(
                _resourcesService.GetString("tournament_winner_prize", room.Culture), result.Winner, prize);
            _bot.Say(roomId, prizeMessage);
        }

        try
        {
            var profileHtml = await _profileService.GetProfileHtmlAsync(result.Winner, roomId, cancellationToken);
            _bot.Say(roomId, $"/addhtmlbox {profileHtml.RemoveNewlines()}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error displaying winner profile for {Winner} in room {RoomId}", result.Winner, roomId);
        }
    }
}
