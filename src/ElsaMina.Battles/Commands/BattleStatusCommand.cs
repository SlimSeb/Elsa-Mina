using System.Globalization;
using ElsaMina.Battles.Replays;
using ElsaMina.Battles.Results;
using ElsaMina.Battles.Strategies;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.UserData;
using ElsaMina.Core.Utils;

namespace ElsaMina.Battles.Commands;

/// <summary>
/// What the battle bot is doing: its strategy, laddering, the battles it plays, its ladder ratings, its results
/// since it started and whether its battles are saved.
/// </summary>
[NamedCommand("battlestatus", "battleinfo", "ladderstatus")]
public class BattleStatusCommand : Command
{
    private const int LABEL_WIDTH = 12;
    private const string NONE = "none";

    private readonly ILadderingService _ladderingService;
    private readonly IBattleService _battleService;
    private readonly IBattleDecisionManager _battleDecisionManager;
    private readonly IBattleResultsService _battleResultsService;
    private readonly ILadderRatingService _ladderRatingService;
    private readonly IReplaySavingService _replaySavingService;
    private readonly IClockService _clockService;

    public BattleStatusCommand(ILadderingService ladderingService,
        IBattleService battleService,
        IBattleDecisionManager battleDecisionManager,
        IBattleResultsService battleResultsService,
        ILadderRatingService ladderRatingService,
        IReplaySavingService replaySavingService,
        IClockService clockService)
    {
        _ladderingService = ladderingService;
        _battleService = battleService;
        _battleDecisionManager = battleDecisionManager;
        _battleResultsService = battleResultsService;
        _ladderRatingService = ladderRatingService;
        _replaySavingService = replaySavingService;
        _clockService = clockService;
    }

    public override bool IsWhitelistOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var now = _clockService.CurrentUtcDateTimeOffset;
        var laddering = _ladderingService.Status;
        var records = _battleResultsService.GetRecords();
        var formats = records
            .Select(record => record.Format)
            .Prepend(laddering.Format)
            .Where(format => !string.IsNullOrEmpty(format))
            .Select(format => format.ToLowerAlphaNum())
            .Distinct()
            .ToList();

        var lines = new List<string>
        {
            Line("Strategy", _battleDecisionManager.ActiveStrategy.GetName()),
            Line("Laddering", DescribeLaddering(laddering, now)),
            Line("Playing", DescribeBattlesInProgress())
        };
        lines.AddRange(await DescribeRatingsAsync(formats, cancellationToken));
        lines.AddRange(DescribeRecords(records));
        lines.Add(Line("Last battle", DescribeLastResult(now)));
        lines.Add(Line("Replays", DescribeReplays(_replaySavingService.Status)));
        context.Reply($"!code {string.Join('\n', lines)}");
    }

    private static string Line(string label, string value) =>
        $"{(string.IsNullOrEmpty(label) ? "" : label + ":").PadRight(LABEL_WIDTH)} {value}";

    private static string DescribeLaddering(LadderingStatus status, DateTimeOffset now)
    {
        if (!status.IsLaddering)
        {
            return "off";
        }

        var duration = status.Since == null ? "" : $" for {FormatDuration(now - status.Since.Value)}";
        return $"{status.Format}{duration}, {DescribeSearch(status, now)}";
    }

    private static string DescribeSearch(LadderingStatus status, DateTimeOffset now)
    {
        if (status.IsInGame)
        {
            return "in a battle";
        }

        if (status.IsSearching)
        {
            return "searching for an opponent";
        }

        if (status.IsWaitingForConfirmation)
        {
            return "search sent, waiting for the server to accept it";
        }

        if (status.NextSearchAt == null)
        {
            return "idle";
        }

        var reason = status.FailedSearchCount > 0
            ? $"the server did not accept the last {status.FailedSearchCount}"
            : "Showdown's search rate limit";
        return $"next search in {FormatDuration(status.NextSearchAt.Value - now)} ({reason})";
    }

    private string DescribeBattlesInProgress()
    {
        var battles = _battleService.GetBattlesInProgress();
        return battles.Count == 0
            ? NONE
            : string.Join(", ", battles.Select(battle => $"{battle.RoomId} (turn {battle.Tracked.Turn})"));
    }

    private async Task<List<string>> DescribeRatingsAsync(List<string> formats, CancellationToken cancellationToken)
    {
        if (formats.Count == 0)
        {
            return [];
        }

        var ratings = await _ladderRatingService.GetRatingsAsync(cancellationToken);
        if (ratings == null)
        {
            return [Line("Ladder", "unavailable, pokemonshowdown.com did not answer")];
        }

        return formats
            .Select((format, index) => Line(index == 0 ? "Ladder" : "",
                ratings.TryGetValue(format, out var rating)
                    ? $"{format}: {DescribeRating(rating)}"
                    : $"{format}: not rated yet"))
            .ToList();
    }

    private static string DescribeRating(UserDataRankingDto rating) => string.Create(CultureInfo.InvariantCulture,
        $"Elo {rating.Elo:F0}, GXE {rating.Gxe:F1}, Glicko {rating.Rpr:F0} ± {rating.Rprd:F0}, {rating.Wins}-{rating.Losses}");

    private static List<string> DescribeRecords(IReadOnlyList<BattleRecord> records)
    {
        if (records.Count == 0)
        {
            return [Line("Session", "no battle played since the bot started")];
        }

        var wins = records.Sum(record => record.Wins);
        var losses = records.Sum(record => record.Losses);
        var ties = records.Sum(record => record.Ties);
        var played = wins + losses + ties;
        var lines = new List<string>
        {
            Line("Session", $"{DescribeScore(wins, losses, ties)} in {played} {(played == 1 ? "battle" : "battles")}")
        };
        lines.AddRange(records.Select(record => Line("",
            $"{record.Format}, {record.Strategy.GetName()}: {DescribeScore(record.Wins, record.Losses, record.Ties)}")));
        return lines;
    }

    private static string DescribeScore(int wins, int losses, int ties)
    {
        var score = ties > 0 ? $"{wins}-{losses}-{ties}" : $"{wins}-{losses}";
        var winRate = 100.0 * wins / (wins + losses + ties);
        return string.Create(CultureInfo.InvariantCulture, $"{score} ({winRate:F1}% won)");
    }

    private string DescribeLastResult(DateTimeOffset now)
    {
        var result = _battleResultsService.LastResult;
        if (result == null)
        {
            return NONE;
        }

        var outcome = result.Outcome switch
        {
            BattleOutcome.Won => "won",
            BattleOutcome.Lost => "lost",
            _ => "tied"
        };
        return $"{outcome} {result.RoomId} in {result.Turns} turns with {result.Strategy.GetName()}, " +
               $"{FormatDuration(now - result.EndedAt)} ago";
    }

    private static string DescribeReplays(ReplaySavingStatus status)
    {
        var last = status.LastSaved == null ? "" : $", last {status.LastSaved}";
        var counts = $"{status.SavedCount} saved, {status.FailedCount} failed, {status.WaitingCount} waiting{last}";
        if (status.IsSaving)
        {
            return $"saving to {status.Directory} ({counts})";
        }

        return status.SavedCount + status.FailedCount + status.WaitingCount == 0
            ? $"off, -savereplays saves them to {status.Directory}"
            : $"off ({counts}, in {status.Directory})";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays}d {duration.Hours}h";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{duration.Hours}h {duration.Minutes:D2}m";
        }

        return duration.TotalMinutes >= 1 ? $"{duration.Minutes}m {duration.Seconds:D2}s" : $"{duration.Seconds}s";
    }
}
