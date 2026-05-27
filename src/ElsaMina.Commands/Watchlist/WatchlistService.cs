using System.Text.RegularExpressions;
using System.Web;
using ElsaMina.Core;
using ElsaMina.Core.Services;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.System;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Watchlist;

public class WatchlistService : IWatchlistService
{
    private static readonly TimeSpan STAFF_INTRO_FETCH_TIMEOUT = TimeSpan.FromSeconds(5);

    private static readonly Regex INFOBOX_OPEN_TAG =
        new(@"<div class=""infobox"">", RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private static readonly Regex INFOBOX_CLOSE_TAG =
        new(@"</div>", RegexOptions.Compiled, Constants.REGEX_MATCH_TIMEOUT);

    private static readonly Regex WATCHLIST_DIV = new(@"(<div class=""watchlist"">).*?(</div>)",
        RegexOptions.Compiled | RegexOptions.Singleline, Constants.REGEX_MATCH_TIMEOUT);

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IBot _bot;
    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;
    private readonly PendingQueryRequestsManager<string, string> _pendingStaffIntroRequests;

    public WatchlistService(IBotDbContextFactory dbContextFactory, IBot bot, IHttpService httpService,
        ISystemService systemService, IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _bot = bot;
        _httpService = httpService;
        _configuration = configuration;
        _pendingStaffIntroRequests = new PendingQueryRequestsManager<string, string>(
            systemService,
            STAFF_INTRO_FETCH_TIMEOUT,
            () => null);
    }

    public async Task<Dictionary<string, string>> GetWatchlistAsync(string roomId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entries = await dbContext.WatchlistEntries
            .Where(entry => entry.RoomId == roomId)
            .ToListAsync(cancellationToken);
        return entries.ToDictionary(entry => entry.UserId, entry => entry.Rank);
    }

    public async Task AddToWatchlistAsync(string roomId, string user, string rank,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.WatchlistEntries.FindAsync([roomId, user], cancellationToken);
        if (existing != null)
        {
            existing.Rank = rank;
        }
        else
        {
            await dbContext.WatchlistEntries.AddAsync(
                new WatchlistEntry { RoomId = roomId, UserId = user, Rank = rank }, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveFromWatchlistAsync(string roomId, string user, string rank,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await dbContext.WatchlistEntries.FindAsync([roomId, user], cancellationToken);
        if (entry == null || entry.Rank != rank)
        {
            return false;
        }

        dbContext.WatchlistEntries.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task FetchAndUpdateStaffIntroAsync(string roomId, CancellationToken cancellationToken = default)
    {
        _bot.Say(roomId, "/staffintro");
        var currentIntro = await _pendingStaffIntroRequests.AddOrReplace(roomId, cancellationToken);
        if (currentIntro == null)
        {
            Log.Warning("Staff intro fetch timed out for room {RoomId}", roomId);
            return;
        }

        var watchlist = await GetWatchlistAsync(roomId, cancellationToken);
        var watchlistHtml = GenerateWatchlistHtml(watchlist);
        var updatedIntro = WATCHLIST_DIV.Replace(currentIntro, $"$1 {watchlistHtml}$2");
        _bot.Say(roomId, $"/staffintro {updatedIntro}");
    }

    public void HandleReceivedStaffIntro(string roomId, string htmlContent)
    {
        var cleaned = INFOBOX_OPEN_TAG.Replace(htmlContent, "", 1);
        cleaned = INFOBOX_CLOSE_TAG.Replace(cleaned, "", 1);
        _pendingStaffIntroRequests.TryResolve(roomId, cleaned);
    }

    public async Task SendDiscordNotificationAsync(string roomId, string message,
        CancellationToken cancellationToken = default)
    {
        if (!_configuration.DiscordWebhooks.TryGetValue(roomId, out var webhookUrl))
        {
            return;
        }

        var payload = new
        {
            username = "Elsa Mina",
            avatar_url = "https://play.pokemonshowdown.com/sprites/trainers/lusamine.png",
            embeds = new[]
            {
                new { title = "Room Update", description = message, color = 3066993 }
            }
        };

        try
        {
            await _httpService.SendAsync<object>(
                HttpRequest.Post(webhookUrl).WithJsonBody(payload), cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to send Discord webhook notification for room {RoomId}", roomId);
        }
    }

    private static string GenerateWatchlistHtml(Dictionary<string, string> watchlist)
    {
        var parts = watchlist.Select(kvp =>
        {
            var color = kvp.Key.ToColorHexCodeWithCustoms();
            var escapedUser = HttpUtility.HtmlEncode(kvp.Key);
            return
                $"""<strong class="username {escapedUser}" style="color: {color};">{kvp.Value}{escapedUser}</strong>""";
        });
        return string.Join(", ", parts);
    }
}