using System.Collections.Concurrent;
using System.Globalization;
using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.RoomUserData;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Tournaments.Betting;

public class TournamentBettingService : ITournamentBettingService
{
    private sealed record ActiveBet(string BettorId, string TargetPlayerId);

    private const int BETTING_WINDOW_SECONDS = 60;
    private const string HTML_ID_PREFIX = "betting-";

    private readonly ConcurrentDictionary<string, string[]> _activePlayers = new();
    private readonly ConcurrentDictionary<string, List<ActiveBet>> _activeBets = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _closingTimes = new();
    private readonly CultureInfo _defaultCulture;

    private readonly IBot _bot;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IResourcesService _resourcesService;
    private readonly IRoomsManager _roomsManager;
    private readonly IClockService _clockService;
    private readonly IBotDbContextFactory _botDbContextFactory;
    private readonly IRoomUserDataService _roomUserDataService;

    public TournamentBettingService(IBot bot, ITemplatesManager templatesManager, IConfiguration configuration,
        IResourcesService resourcesService, IRoomsManager roomsManager, IClockService clockService,
        IBotDbContextFactory botDbContextFactory, IRoomUserDataService roomUserDataService)
    {
        _bot = bot;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _resourcesService = resourcesService;
        _roomsManager = roomsManager;
        _clockService = clockService;
        _botDbContextFactory = botDbContextFactory;
        _roomUserDataService = roomUserDataService;

        _defaultCulture = new CultureInfo(configuration.DefaultLocaleCode);
    }

    public async Task AnnounceBetsAsync(string[] players, string roomId,
        CancellationToken cancellationToken = default)
    {
        _activePlayers[roomId] = players.Select(p => p.ToLowerAlphaNum()).ToHashSet().ToArray();
        _activeBets[roomId] = [];
        _closingTimes[roomId] = _clockService.CurrentUtcDateTimeOffset.AddSeconds(BETTING_WINDOW_SECONDS);

        var template = await BuildTemplateAsync(roomId, isBettingOpen: true);
        _bot.Say(roomId, $"/adduhtml {HTML_ID_PREFIX}{roomId}, {template}");
    }

    public async Task<BetPlacementError> PlaceBetAsync(string bettorId, string targetPlayerId, string roomId,
        CancellationToken cancellationToken = default)
    {
        if (!_activePlayers.TryGetValue(roomId, out var players) || !_activeBets.TryGetValue(roomId, out var bets))
        {
            return BetPlacementError.NoBettingSession;
        }

        if (_closingTimes.TryGetValue(roomId, out var closingTime) && _clockService.CurrentUtcDateTimeOffset > closingTime)
        {
            return BetPlacementError.BettingClosed;
        }

        if (!players.Contains(targetPlayerId))
        {
            return BetPlacementError.InvalidPlayer;
        }

        if (bets.Any(bet => bet.BettorId == bettorId))
        {
            return BetPlacementError.AlreadyBet;
        }

        bets.Add(new ActiveBet(bettorId, targetPlayerId));
        await RefreshHtmlAsync(roomId);
        return BetPlacementError.Success;
    }

    public async Task<int> CancelBetAsync(string bettorId, string roomId, string targetPlayerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_activeBets.TryGetValue(roomId, out var bets))
        {
            return 0;
        }

        var toCancel = targetPlayerId != null
            ? bets.Where(bet => bet.BettorId == bettorId && bet.TargetPlayerId == targetPlayerId).ToList()
            : bets.Where(bet => bet.BettorId == bettorId).ToList();

        foreach (var bet in toCancel)
        {
            bets.Remove(bet);
        }

        if (toCancel.Count > 0)
        {
            await RefreshHtmlAsync(roomId);
        }

        return toCancel.Count;
    }

    public async Task ResolveBetsAsync(string winnerId, string roomId, CancellationToken cancellationToken = default)
    {
        if (!_activeBets.TryGetValue(roomId, out var bets) || bets.Count == 0)
        {
            CleanUp(roomId);
            return;
        }

        var culture = _roomsManager.GetRoom(roomId)?.Culture;

        var correctBettors = bets
            .Where(bet => bet.TargetPlayerId == winnerId)
            .Select(bet => bet.BettorId)
            .Distinct()
            .ToList();

        string message;
        if (correctBettors.Count > 0)
        {
            var names = string.Join(", ", correctBettors);
            message = string.Format(
                _resourcesService.GetString("bet_resolution_correct_guesses", culture), winnerId, names);
        }
        else
        {
            message = string.Format(
                _resourcesService.GetString("bet_resolution_nobody_correct", culture), winnerId);
        }

        _bot.Say(roomId, $"/addhtmlbox <div>{message}</div>");

        var allBettors = bets.Select(bet => bet.BettorId).Distinct().ToList();
        var correctBettorSet = correctBettors.ToHashSet();
        CleanUp(roomId);

        try
        {
            await using var dbContext = await _botDbContextFactory.CreateDbContextAsync(cancellationToken);
            foreach (var bettorId in allBettors)
            {
                await _roomUserDataService.GetOrCreateRoomSpecificUserDataAsync(roomId, bettorId, cancellationToken);

                var record = await dbContext.BetRecords.FindAsync([bettorId, roomId], cancellationToken);
                if (record == null)
                {
                    record = new BetRecord { UserId = bettorId, RoomId = roomId };
                    await dbContext.BetRecords.AddAsync(record, cancellationToken);
                }

                record.TotalBetsCount++;
                if (correctBettorSet.Contains(bettorId))
                {
                    record.CorrectBetsCount++;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving bet records for room {RoomId}", roomId);
        }
    }

    public Task ReturnBetsAsync(string roomId, CancellationToken cancellationToken = default)
    {
        CleanUp(roomId);
        return Task.CompletedTask;
    }

    private async Task RefreshHtmlAsync(string roomId)
    {
        var isBettingOpen = _closingTimes.TryGetValue(roomId, out var closingTime) &&
                            _clockService.CurrentUtcDateTimeOffset <= closingTime;
        var template = await BuildTemplateAsync(roomId, isBettingOpen);
        _bot.Say(roomId, $"/changeuhtml {HTML_ID_PREFIX}{roomId}, {template}");
    }

    private async Task<string> BuildTemplateAsync(string roomId, bool isBettingOpen)
    {
        var culture = _roomsManager.GetRoom(roomId)?.Culture;
        var players = _activePlayers.TryGetValue(roomId, out var p) ? p : [];
        var bets = _activeBets.TryGetValue(roomId, out var b) ? b : [];

        var betsByPlayer = players.ToDictionary(
            player => player,
            player => (IReadOnlyList<string>)bets
                .Where(bet => bet.TargetPlayerId == player)
                .Select(bet => bet.BettorId)
                .ToList());

        var viewModel = new BettingAnnouncementViewModel
        {
            Culture = culture ?? _defaultCulture,
            Players = players,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = roomId,
            SecondsToClose = BETTING_WINDOW_SECONDS,
            IsBettingOpen = isBettingOpen,
            BetsByPlayer = betsByPlayer
        };

        var template = await _templatesManager.GetTemplateAsync("Tournaments/Betting/BettingAnnouncement", viewModel);
        return template.RemoveNewlines();
    }

    private void CleanUp(string roomId)
    {
        _activePlayers.TryRemove(roomId, out _);
        _activeBets.TryRemove(roomId, out _);
        _closingTimes.TryRemove(roomId, out _);
    }
}
