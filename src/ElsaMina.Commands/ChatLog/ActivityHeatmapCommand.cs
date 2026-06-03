using System.Globalization;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Utils;
using ElsaMina.FileSharing;
using ElsaMina.Logging;
using ScottPlot;

namespace ElsaMina.Commands.ChatLog;

[NamedCommand("activityheatmap", Aliases = ["heatmap", "activitymap"])]
public class ActivityHeatmapCommand : Command
{
    private const int CHART_WIDTH = 720;
    private const int CHART_HEIGHT = 340;
    private const int DAYS_PER_WEEK = 7;
    private const int HOURS_PER_DAY = 24;

    private readonly IFileSharingService _fileSharingService;
    private readonly IRoomsManager _roomsManager;

    public ActivityHeatmapCommand(IFileSharingService fileSharingService, IRoomsManager roomsManager)
    {
        _fileSharingService = fileSharingService;
        _roomsManager = roomsManager;
    }

    public override Rank RequiredRank => Rank.Admin;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "activity_heatmap_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = context.Target.Split(',', StringSplitOptions.TrimEntries);
        var userId = parts.Length >= 1 ? parts[0].ToLowerAlphaNum() : string.Empty;

        if (string.IsNullOrWhiteSpace(userId))
        {
            ReplyLocalizedHelpMessage(context);
            return;
        }

        // Remaining arguments are an optional room and an optional "yyyy-MM" month, in any order.
        var now = DateTime.UtcNow;
        var roomId = context.RoomId;
        var year = now.Year;
        var month = now.Month;
        foreach (var part in parts.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            if (TryParseMonth(part, out var parsedYear, out var parsedMonth))
            {
                year = parsedYear;
                month = parsedMonth;
            }
            else
            {
                roomId = part.ToLowerAlphaNum();
            }
        }

        try
        {
            var keys = await _fileSharingService.ListFilesAsync(
                ChatLogHelpers.GetS3MonthPrefix(roomId, year, month), cancellationToken);

            if (keys.Count == 0)
            {
                context.ReplyRankAwareLocalizedMessage("activity_heatmap_no_logs");
                return;
            }

            // Logs are stored in UTC; bin activity in the room's local time zone.
            var timeZone = _roomsManager.GetRoom(roomId)?.TimeZone ?? TimeZoneInfo.Utc;

            // counts[dayOfWeekIndex, hour] where dayOfWeekIndex is 0 = Monday ... 6 = Sunday
            var counts = new double[DAYS_PER_WEEK, HOURS_PER_DAY];
            var totalCount = 0;

            foreach (var key in keys)
            {
                if (!TryGetDateFromKey(key, out var date))
                {
                    continue;
                }

                await using var stream = await _fileSharingService.GetFileAsync(key, cancellationToken);
                if (stream == null)
                {
                    continue;
                }

                using var reader = new StreamReader(stream);
                while (await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    if (!ChatLogHelpers.TryParseLine(line, out var username, out _)
                        || username.ToLowerAlphaNum() != userId
                        || !TryParseTimeOfDay(line, out var timeOfDay))
                    {
                        continue;
                    }

                    // Convert the UTC timestamp to the room's local time; the day of week may shift.
                    var utc = DateTime.SpecifyKind(date.ToDateTime(timeOfDay), DateTimeKind.Utc);
                    var local = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);
                    var dayIndex = ((int)local.DayOfWeek + 6) % 7; // Sunday(0) -> 6, Monday(1) -> 0

                    counts[dayIndex, local.Hour]++;
                    totalCount++;
                }
            }

            if (totalCount == 0)
            {
                context.ReplyRankAwareLocalizedMessage("activity_heatmap_no_activity", parts[0]);
                return;
            }

            var title = context.GetString("activity_heatmap_chart_title", parts[0], roomId);
            var timeZoneLabel = GetTimeZoneLabel(timeZone);
            var pngBytes = GenerateHeatmap(context, title, counts, context.Culture, timeZoneLabel);

            var fileName = $"heatmaps/heatmap-{userId}-{roomId}-{year:D4}{month:D2}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.png";
            var url = await _fileSharingService.CreateFileAsync(pngBytes, fileName,
                description: $"Activity heatmap for {parts[0]} in {roomId}",
                mimeType: "image/png",
                cancellationToken: cancellationToken);

            if (url == null)
            {
                context.ReplyRankAwareLocalizedMessage("activity_heatmap_upload_failed");
                return;
            }

            context.ReplyHtml(
                $"""<a href="{url}" target="_blank" rel="noopener"><img src="{url}" width={CHART_WIDTH} height={CHART_HEIGHT} style="max-width:100%;border-radius:6px" /></a>""",
                rankAware: true);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to generate activity heatmap for {UserId} in {RoomId}", userId, roomId);
            await context.HandleErrorAsync(exception, cancellationToken);
        }
    }

    private static byte[] GenerateHeatmap(IContext context, string title, double[,] counts, CultureInfo culture,
        string timeZoneLabel)
    {
        var xLabel = context.GetString("activity_heatmap_x_label", timeZoneLabel);
        var colorBarLabel = context.GetString("activity_heatmap_colorbar_label");
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;

            var plot = new Plot();

            var heatmap = plot.Add.Heatmap(counts);
            heatmap.Colormap = new ScottPlot.Colormaps.Viridis();
            // Map data cells onto coordinates: x in [0, 24] (hours), y in [0, 7] (days).
            heatmap.Extent = new CoordinateRect(0, HOURS_PER_DAY, 0, DAYS_PER_WEEK);

            var colorBar = plot.Add.ColorBar(heatmap);
            colorBar.Label = colorBarLabel;

            // Hour ticks every 3 hours, centered on each cell column.
            var hourTicks = new ScottPlot.TickGenerators.NumericManual();
            for (var hour = 0; hour < HOURS_PER_DAY; hour += 3)
            {
                hourTicks.AddMajor(hour + 0.5, hour.ToString("D2", culture));
            }
            plot.Axes.Bottom.TickGenerator = hourTicks;

            // Day ticks: row 0 (Monday) renders at the top, so its band center is at y = 6.5.
            var dayTicks = new ScottPlot.TickGenerators.NumericManual();
            var abbreviatedDayNames = culture.DateTimeFormat.AbbreviatedDayNames;
            for (var dayIndex = 0; dayIndex < DAYS_PER_WEEK; dayIndex++)
            {
                var dayOfWeek = (dayIndex + 1) % 7; // Monday(0) -> DayOfWeek.Monday(1), Sunday(6) -> 0
                dayTicks.AddMajor(DAYS_PER_WEEK - (dayIndex + 0.5), abbreviatedDayNames[dayOfWeek]);
            }
            plot.Axes.Left.TickGenerator = dayTicks;

            plot.Title(title);
            plot.XLabel(xLabel);

            return plot.GetImage(CHART_WIDTH, CHART_HEIGHT).GetImageBytes(ImageFormat.Png);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    // Argument format: "yyyy-MM", e.g. "2026-05".
    private static bool TryParseMonth(string value, out int year, out int month)
    {
        year = 0;
        month = 0;
        if (!DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        year = parsed.Year;
        month = parsed.Month;
        return true;
    }

    // Filename is "yyyy-MM-dd"
    private static bool TryGetDateFromKey(string key, out DateOnly date)
    {
        var fileName = Path.GetFileNameWithoutExtension(key);
        return DateOnly.TryParseExact(fileName, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
    }

    // Line format: "[HH:mm:ss UTC] username: message"
    private static bool TryParseTimeOfDay(string line, out TimeOnly timeOfDay)
    {
        timeOfDay = default;
        return line.Length >= 9 && line[0] == '['
               && TimeOnly.TryParseExact(line.AsSpan(1, 8), "HH:mm:ss", CultureInfo.InvariantCulture,
                   DateTimeStyles.None, out timeOfDay);
    }

    // A short, unambiguous label for the chart axis, e.g. "UTC+02:00" (or "UTC" for the zero offset).
    private static string GetTimeZoneLabel(TimeZoneInfo timeZone)
    {
        var offset = timeZone.GetUtcOffset(DateTime.UtcNow);
        if (offset == TimeSpan.Zero)
        {
            return "UTC";
        }

        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();
        return $"UTC{sign}{absolute.Hours:D2}:{absolute.Minutes:D2}";
    }
}
