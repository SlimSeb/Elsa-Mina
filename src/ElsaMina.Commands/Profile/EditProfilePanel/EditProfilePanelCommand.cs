using System.Globalization;
using System.Text;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

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
            ["teal"] = "#67aaaa73",
            ["darkgreen"] = "#2d732d73",
            ["indigo"] = "#6757aa73",
            ["gray"] = "#7a7a7a73",
            ["white"] = "#d5d5d573",
            ["brown"] = "#7a573073",
            ["rainbow"] = "linear-gradient(135deg, #ff666673, #ffaa4473, #ffff6673, #66cc6673, #6699ff73, #bb66ff73, #ff66cc73)",
            ["sunset"] = "linear-gradient(135deg, #aa334473, #ff666673, #ffaa4473, #ffcc8873)",
            ["ocean"] = "linear-gradient(135deg, #2d4f7373, #6699ff73, #66cccc73, #66ffcc73)",
            ["fire"] = "linear-gradient(135deg, #aa333373, #ff666673, #ffaa4473, #ffff6673)",
            ["aurora"] = "linear-gradient(135deg, #44aa6673, #66ccaa73, #6699ff73, #bb66ff73)",
            ["candy"] = "linear-gradient(135deg, #ff88cc73, #cc88ff73, #8899ff73)",
            ["pastel"] = "linear-gradient(135deg, #55cdfc73, #f7a8b873, #ffffff73, #f7a8b873, #55cdfc73)",
            ["wooden"] = "linear-gradient(135deg, #3b241673, #8b5a2b73, #c6864273, #f2c07873)",
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
            ["teal"] = "#6aa",
            ["darkgreen"] = "#363",
            ["indigo"] = "#67a",
            ["gray"] = "#888",
            ["white"] = "#bbb",
            ["brown"] = "#763",
            ["rainbow"] = "#a6f",
            ["sunset"] = "#f86",
            ["ocean"] = "#5ac",
            ["fire"] = "#f74",
            ["aurora"] = "#5b8",
            ["candy"] = "#c8f",
            ["pastel"] = "#5cf",
            ["wooden"] = "#a63",
        };

    // Only solid colors here: the profile text has to stay readable on top of whichever background
    // the user picked, and gradients cannot be applied to text in Showdown's sanitized HTML.
    public static readonly IReadOnlyDictionary<string, string> PROFILE_TEXT_COLORS =
        new Dictionary<string, string>
        {
            ["blue"] = "#6a9fd8",
            ["darkblue"] = "#4a6fa5",
            ["purple"] = "#b18cd9",
            ["red"] = "#e07070",
            ["orange"] = "#e0a060",
            ["yellow"] = "#e0d060",
            ["green"] = "#7ac77a",
            ["pink"] = "#e08cb8",
            ["black"] = "#202020",
            ["teal"] = "#6ad0d0",
            ["darkgreen"] = "#4a9a4a",
            ["indigo"] = "#8a86e0",
            ["gray"] = "#a0a0a0",
            ["white"] = "#ffffff",
            ["brown"] = "#b08050",
        };

    public static readonly IReadOnlyDictionary<string, string> PROFILE_LABEL_COLORS = PROFILE_TEXT_COLORS;

    public static string GetBorderColor(string backgroundColorValue) =>
        PROFILE_COLORS.FirstOrDefault(kvp => kvp.Value == backgroundColorValue) is { Key: { } key }
            && PROFILE_BORDER_COLORS.TryGetValue(key, out var border)
            ? border
            : StyleConstants.PRIMARY_BORDER_COLOR;

    private readonly IEditProfilePanelService _editProfilePanelService;

    public EditProfilePanelCommand(IEditProfilePanelService editProfilePanelService)
    {
        _editProfilePanelService = editProfilePanelService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = string.IsNullOrWhiteSpace(context.Target)
            ? context.RoomId
            : context.Target.Trim().ToLowerAlphaNum();

        return _editProfilePanelService.SendPanelAsync(context, roomId, cancellationToken);
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
