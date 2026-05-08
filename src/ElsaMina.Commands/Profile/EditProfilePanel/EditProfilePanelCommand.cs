using System.Globalization;
using System.Text;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Profile.EditProfilePanel;

[NamedCommand("editprofilepanel", Aliases = ["edit-profile-panel", "profilepanel", "profile-panel"])]
public class EditProfilePanelCommand : Command
{
    private const int REGIONAL_INDICATOR_BASE = 0x1F1E6 - 'A';

    public static readonly IReadOnlyDictionary<string, string> PROFILE_COLORS =
        new Dictionary<string, string>
        {
            ["blue"] = StyleConstants.PRIMARY_BACKGROUND_COLOR,
            ["darkblue"] = "#2d4f7373",
            ["purple"] = "#8867aa73",
            ["red"] = "#aa676773",
            ["orange"] = "#aa886773",
            ["yellow"] = "#aaaa6773",
            ["green"] = "#67aa6773",
            ["pink"] = "#aa678873",
            ["black"] = "#40404073",
        };

    public static readonly IReadOnlyDictionary<string, string> PROFILE_BORDER_COLORS =
        new Dictionary<string, string>
        {
            ["blue"] = StyleConstants.PRIMARY_BORDER_COLOR,
            ["darkblue"] = "#247",
            ["purple"] = "#87a",
            ["red"] = "#a66",
            ["orange"] = "#a87",
            ["yellow"] = "#aa6",
            ["green"] = "#6a6",
            ["pink"] = "#a68",
            ["black"] = "#555",
        };

    public static string GetBorderColor(string backgroundColorValue) =>
        PROFILE_COLORS.FirstOrDefault(kvp => kvp.Value == backgroundColorValue) is { Key: { } key }
            && PROFILE_BORDER_COLORS.TryGetValue(key, out var border)
            ? border
            : StyleConstants.PRIMARY_BORDER_COLOR;

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IRoomsManager _roomsManager;

    public EditProfilePanelCommand(IBotDbContextFactory dbContextFactory,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IRoomsManager roomsManager)
    {
        _dbContextFactory = dbContextFactory;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _roomsManager = roomsManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = string.IsNullOrWhiteSpace(context.Target)
            ? context.RoomId
            : context.Target.Trim().ToLowerAlphaNum();

        var room = _roomsManager.GetRoom(roomId);
        if (context.IsPrivateMessage && room != null)
        {
            context.Culture = room.Culture;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var userId = context.Sender.UserId;
        var storedUser = await dbContext.RoomUsers
            .FirstOrDefaultAsync(u => u.Id == userId && u.RoomId == roomId, cancellationToken);

        var viewModel = new EditProfilePanelViewModel
        {
            Culture = context.Culture,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = roomId,
            UserId = userId,
            CurrentEmoji = storedUser?.ProfileEmoji ?? string.Empty,
            CurrentBackgroundColor = storedUser?.ProfileBackgroundColor ?? string.Empty,
        };

        var template = await _templatesManager.GetTemplateAsync(
            "Profile/EditProfilePanel/EditProfilePanel", viewModel);
        context.ReplyHtmlPage($"edit-profile-{userId}", template
            .RemoveNewlines()
            .CollapseAttributeWhitespace()
            .RemoveWhitespacesBetweenTags());
    }
    
    public static IEnumerable<(string Code, string Flag, string Name)> GetAllCountryFlags()
    {
        return CultureInfo
            .GetCultures(CultureTypes.SpecificCultures)
            .Select(cultureInfo => new RegionInfo(cultureInfo.Name))
            .DistinctBy(regionInfo => regionInfo.TwoLetterISORegionName)
            .OrderBy(regionInfo => regionInfo.EnglishName)
            .Select(regionInfo => (
                Code: regionInfo.TwoLetterISORegionName,
                Flag: ToFlagEmoji(regionInfo.TwoLetterISORegionName),
                Name: regionInfo.EnglishName
            ))
            .Where(tpl => tpl.Flag != null);
    }

    private static string ToFlagEmoji(string countryCode)
    {
        if (countryCode?.Length != 2)
        {
            return null;
        }

        countryCode = countryCode.ToUpperInvariant();

        if (!countryCode.All(char.IsAsciiLetter))
        {
            return null;
        }
        var sb = new StringBuilder();
        foreach (var chr in countryCode)
        {
            sb.Append(char.ConvertFromUtf32(REGIONAL_INDICATOR_BASE + chr));
        }

        return sb.ToString();
    }
}
