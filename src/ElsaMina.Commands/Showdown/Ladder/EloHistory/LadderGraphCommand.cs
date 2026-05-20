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

[NamedCommand("ladderhistory", "laddergraph", "elograph")]
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

            var pngBytes = GenerateChart(xs, ys, parts[1], format);

            var fileName = $"elograph-{userId}-{format}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.png";
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

    private static byte[] GenerateChart(double[] xs, double[] ys, string username, string format)
    {
        var plot = new Plot();

        var scatter = plot.Add.Scatter(xs, ys);
        scatter.LineWidth = 2;
        scatter.MarkerSize = 5;

        plot.Axes.DateTimeTicksBottom();
        plot.Title($"ELO history — {username} ({format})");
        plot.XLabel("Date");
        plot.YLabel("ELO");

        return plot.GetImage(CHART_WIDTH, CHART_HEIGHT).GetImageBytes(ImageFormat.Png);
    }
}
