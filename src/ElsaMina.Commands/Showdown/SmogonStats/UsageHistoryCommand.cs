using System.Globalization;
using ElsaMina.Cloud;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Smogon;
using ElsaMina.Core.Utils;
using ElsaMina.Logging;
using ScottPlot;

namespace ElsaMina.Commands.Showdown.SmogonStats;

[NamedCommand("usagehistory", Aliases = ["usagegraph", "usagetrend", "popularity"])]
public class UsageHistoryCommand : Command
{
    private const Level DEFAULT_PLAYER_LEVEL = Level.VeryHigh;
    private const int DEFAULT_MONTHS_COUNT = 12;
    private const int MINIMUM_MONTHS_COUNT = 2;
    private const int MAXIMUM_MONTHS_COUNT = 36;
    private const int CHART_WIDTH = 600;
    private const int CHART_HEIGHT = 300;
    private const string MONTH_FORMAT = "yyyy-MM";

    private readonly ISmogonUsageDataProvider _smogonUsageDataProvider;
    private readonly IFileSharingService _fileSharingService;
    private readonly IClockService _clockService;

    public UsageHistoryCommand(ISmogonUsageDataProvider smogonUsageDataProvider,
        IFileSharingService fileSharingService,
        IClockService clockService)
    {
        _smogonUsageDataProvider = smogonUsageDataProvider;
        _fileSharingService = fileSharingService;
        _clockService = clockService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "usage_history_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            ReplyLocalizedHelpMessage(context, rankAware: true);
            return;
        }

        var pokemonName = parts[0];
        var format = parts[1].ToLowerAlphaNum();

        var monthsCount = DEFAULT_MONTHS_COUNT;
        if (parts.Length > 2 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var parsedMonthsCount))
        {
            monthsCount = Math.Clamp(parsedMonthsCount, MINIMUM_MONTHS_COUNT, MAXIMUM_MONTHS_COUNT);
        }

        var playerLevel = DEFAULT_PLAYER_LEVEL;
        if (parts.Length > 3 && Enum.TryParse<Level>(parts[3], ignoreCase: true, out var parsedLevel))
        {
            playerLevel = parsedLevel;
        }

        try
        {
            var dataPoints = await GetUsageHistoryAsync(pokemonName, format, monthsCount, playerLevel,
                cancellationToken);

            if (dataPoints.Count < MINIMUM_MONTHS_COUNT)
            {
                context.ReplyRankAwareLocalizedMessage("usage_history_not_enough_data", pokemonName, format);
                return;
            }

            // Smogon's own spelling, so the chart shows "Ogerpon-Wellspring" even if "ogerponwellspring" was typed.
            var displayName = dataPoints[0].PokemonName;
            var pngBytes = GenerateChart(context, displayName, format, dataPoints);

            var fileName = $"usagegraphs/usagegraph-{displayName.ToLowerAlphaNum()}-{format}-" +
                           $"{_clockService.CurrentUtcDateTimeOffset.ToUnixTimeSeconds()}.png";
            var url = await _fileSharingService.CreateFileAsync(pngBytes, fileName,
                description: $"Usage history for {displayName} in {format}",
                mimeType: "image/jpeg",
                cancellationToken: cancellationToken);

            if (url == null)
            {
                context.ReplyRankAwareLocalizedMessage("usage_history_upload_failed");
                return;
            }

            context.ReplyHtml(
                $"""<a href="{url}" target="_blank" rel="noopener"><img src="{url}" width={CHART_WIDTH} height={CHART_HEIGHT} style="max-width:100%;border-radius:6px" /></a>""",
                rankAware: true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Usage history command failed for {Pokemon} in {Format} ({Months} months, {Level})",
                pokemonName, format, monthsCount, playerLevel);
            await context.HandleErrorAsync(exception, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<UsageHistoryPoint>> GetUsageHistoryAsync(string pokemonName, string format,
        int monthsCount, Level playerLevel, CancellationToken cancellationToken)
    {
        var normalizedPokemonName = pokemonName.ToLowerAlphaNum();
        var months = GetMonths(monthsCount);

        var points = await Task.WhenAll(months.Select(month =>
            GetMonthUsageAsync(month, normalizedPokemonName, format, playerLevel, cancellationToken)));

        return points
            .OfType<UsageHistoryPoint>()
            .OrderBy(point => point.Month)
            .ToList();
    }

    private async Task<UsageHistoryPoint> GetMonthUsageAsync(DateTime month, string normalizedPokemonName,
        string format, Level playerLevel, CancellationToken cancellationToken)
    {
        var monthKey = month.ToString(MONTH_FORMAT, CultureInfo.InvariantCulture);
        try
        {
            var ranking = await _smogonUsageDataProvider.GetUsageRankingAsync(monthKey, format, playerLevel,
                cancellationToken);

            var entry = ranking?.FirstOrDefault(rankingEntry =>
                rankingEntry.PokemonName.ToLowerAlphaNum() == normalizedPokemonName);

            return entry == null ? null : new UsageHistoryPoint(month, entry.PokemonName, entry.UsagePercentage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Smogon answers 404 for months where the format had not been published yet : the
            // series simply starts later instead of failing the whole command.
            Log.Debug("No usage ranking for {Format} in {Month} : {Reason}", format, monthKey, exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Les mois en ordre croissant, le dernier etant le mois precedent : Smogon publie les
    /// statistiques d'un mois une fois celui-ci termine.
    /// </summary>
    private IReadOnlyList<DateTime> GetMonths(int monthsCount)
    {
        var currentDate = _clockService.CurrentUtcDateTime;
        var lastPublishedMonth = new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(-1);

        return Enumerable.Range(0, monthsCount)
            .Select(offset => lastPublishedMonth.AddMonths(offset - monthsCount + 1))
            .ToList();
    }

    private static byte[] GenerateChart(IContext context, string pokemonName, string format,
        IReadOnlyList<UsageHistoryPoint> dataPoints)
    {
        var title = context.GetString("usage_history_chart_title", pokemonName, format);
        var xLabel = context.GetString("usage_history_chart_x_label");
        var yLabel = context.GetString("usage_history_chart_y_label");
        var peakLabelFormat = context.GetString("usage_history_peak");
        var latestLabelFormat = context.GetString("usage_history_latest");

        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = context.Culture;

            var xs = dataPoints.Select(point => point.Month.ToOADate()).ToArray();
            var ys = dataPoints.Select(point => point.UsagePercentage).ToArray();

            var plot = new Plot();

            var scatter = plot.Add.Scatter(xs, ys);
            scatter.LineWidth = 2;
            scatter.MarkerSize = 5;

            var peakPoint = dataPoints.MaxBy(point => point.UsagePercentage);
            var latestPoint = dataPoints[^1];
            var peakLine = string.Format(peakLabelFormat, $"{peakPoint.UsagePercentage:F2}",
                peakPoint.Month.ToString(MONTH_FORMAT, CultureInfo.InvariantCulture));
            var latestLine = string.Format(latestLabelFormat, $"{latestPoint.UsagePercentage:F2}",
                latestPoint.Month.ToString(MONTH_FORMAT, CultureInfo.InvariantCulture));

            var annotation = plot.Add.Annotation($"{peakLine}\n{latestLine}");
            // Le texte se pose du cote oppose a la fin de la courbe pour eviter de la recouvrir.
            annotation.Alignment = ys[^1] >= ys[0] ? Alignment.UpperLeft : Alignment.UpperRight;

            plot.Axes.DateTimeTicksBottom();
            // Une courbe d'utilisation ne se lit correctement que depuis zero.
            plot.Axes.SetLimitsY(0, Math.Max(ys.Max() * 1.15, 1));
            plot.Title(title);
            plot.XLabel(xLabel);
            plot.YLabel(yLabel);

            return plot.GetImage(CHART_WIDTH, CHART_HEIGHT).GetImageBytes(ImageFormat.Jpeg);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
