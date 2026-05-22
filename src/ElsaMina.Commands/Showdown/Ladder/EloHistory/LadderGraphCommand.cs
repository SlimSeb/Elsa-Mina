using System.Globalization;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using ElsaMina.FileSharing;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;
using ScottPlot;

namespace ElsaMina.Commands.Showdown.Ladder.EloHistory;

[NamedCommand("ladderhistory", "laddergraph", "elograph", "elotrend", "laddertrend")]
public class LadderGraphCommand : Command
{
    private const int CHART_WIDTH = 600;
    private const int CHART_HEIGHT = 300;

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IFileSharingService _fileSharingService;

    public LadderGraphCommand(IBotDbContextFactory dbContextFactory, IFileSharingService fileSharingService)
    {
        _dbContextFactory = dbContextFactory;
        _fileSharingService = fileSharingService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "ladder_graph_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        var format = parts[0].ToLowerAlphaNum();
        var userId = parts[1].ToLowerAlphaNum();

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var snapshots = await dbContext.LadderEloSnapshots
                .Where(s => s.Format == format && s.UserId == userId)
                .OrderBy(s => s.RecordedAt)
                .ToListAsync(cancellationToken);

            if (snapshots.Count < 2)
            {
                context.ReplyRankAwareLocalizedMessage("ladder_graph_not_enough_data", parts[1]);
                return;
            }

            var xs = snapshots.Select(s => s.RecordedAt.ToOADate()).ToArray();
            var ys = snapshots.Select(s => (double)s.Elo).ToArray();

            var chartTitle = context.GetString("ladder_graph_chart_title", parts[1], parts[0]);
            var xLabel = context.GetString("ladder_graph_chart_x_label");
            var yLabel = context.GetString("ladder_graph_chart_y_label");

            var showTrend = context.Command is "elotrend" or "laddertrend";
            var slopeLabelFormat = context.GetString("ladder_graph_trend_slope");
            var rSquaredLabelFormat = context.GetString("ladder_graph_trend_r_squared");
            var pngBytes = GenerateChart(xs, ys, chartTitle, xLabel, yLabel, context.Culture, showTrend,
                slopeLabelFormat, rSquaredLabelFormat);

            var fileName = $"elographs/elograph-{userId}-{format}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.png";
            var url = await _fileSharingService.CreateFileAsync(pngBytes, fileName,
                description: $"ELO history for {parts[1]} in {format}",
                mimeType: "image/png",
                cancellationToken: cancellationToken);

            if (url == null)
            {
                context.ReplyRankAwareLocalizedMessage("ladder_graph_upload_failed");
                return;
            }

            context.ReplyHtml(
                $"""<a href="{url}" target="_blank" rel="noopener"><img src="{url}" width={CHART_WIDTH} height={CHART_HEIGHT} style="max-width:100%;border-radius:6px" /></a>""",
                rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate ladder ELO graph for {UserId} in {Format}", userId, format);
            await context.HandleErrorAsync(ex, cancellationToken);
        }
    }

    private static byte[] GenerateChart(double[] xs, double[] ys, string title, string xLabel, string yLabel,
        CultureInfo culture, bool showTrend, string slopeLabelFormat, string rSquaredLabelFormat)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;

            var plot = new Plot();

            var scatter = plot.Add.Scatter(xs, ys);
            scatter.LineWidth = 2;
            scatter.MarkerSize = 5;

            if (showTrend)
            {
                var (slope, intercept, rSquared) = ComputeLinearRegression(xs, ys);
                var trendLine = plot.Add.Line(xs[0], slope * xs[0] + intercept, xs[^1], slope * xs[^1] + intercept);
                trendLine.Color = Colors.Red;
                trendLine.LineWidth = 2;

                var slopeLine = string.Format(slopeLabelFormat, $"{slope:+0.##;-0.##}");
                var rSquaredLine = string.Format(rSquaredLabelFormat, $"{rSquared:F4}");
                var annotation = plot.Add.Annotation($"{slopeLine}\n{rSquaredLine}");
                annotation.Alignment = Alignment.UpperRight;
            }

            plot.Axes.DateTimeTicksBottom();
            plot.Title(title);
            plot.XLabel(xLabel);
            plot.YLabel(yLabel);

            return plot.GetImage(CHART_WIDTH, CHART_HEIGHT).GetImageBytes(ImageFormat.Png);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    // In this context, R² answers: "how much of the ELO variance is 
    // explained by time progressing linearly?"
    //  
    // Concretely:
    // - R² near 1: the player's ELO moved almost monotonically in one
    //     direction — the slope is trustworthy. "+5 ELO/day, R²=0.92" 
    // genuinely means steady improvement.
    // - R² near 0: the ELO bounced around a lot and the linear fit is
    // meaningless. "+5 ELO/day, R²=0.03" could just be noise between two
    // similar endpoints.
    //
    //     The practical reading for Showdown laddering:
    //
    // - Most active players will have low R² (0.05–0.3) because ELO
    //     oscillates with win/loss streaks, and that's normal — not a sign
    //     the trend line is wrong, just that laddering is noisy.
    // - A high R² (>0.6) is actually unusual and meaningful: it means the
    //     player was consistently climbing or consistently dropping, with
    // little variance around the trend.
    // - A negative slope with high R² is a red flag: consistent decay,
    //     not just a bad session.
    //
    //     So R² here is less "goodness of fit" in the regression-report sense
    // and more "how consistent was this trend" — which is actually quite
    // natural for players to understand once you frame it that way.
    private static (double slope, double intercept, double rSquared) ComputeLinearRegression(double[] xs, double[] ys)
    {
        var n = xs.Length;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXx = 0.0;
        var sumXy = 0.0;

        for (var i = 0; i < n; i++)
        {
            sumX += xs[i];
            sumY += ys[i];
            sumXx += xs[i] * xs[i];
            sumXy += xs[i] * ys[i];
        }

        var slope = (n * sumXy - sumX * sumY) / (n * sumXx - sumX * sumX);
        var intercept = (sumY - slope * sumX) / n;

        var meanY = sumY / n;
        var ssTot = 0.0;
        var ssRes = 0.0;
        for (var i = 0; i < n; i++)
        {
            ssTot += (ys[i] - meanY) * (ys[i] - meanY);
            ssRes += (ys[i] - (slope * xs[i] + intercept)) * (ys[i] - (slope * xs[i] + intercept));
        }

        var rSquared = ssTot == 0 ? 1.0 : 1.0 - ssRes / ssTot;
        return (slope, intercept, rSquared);
    }
}