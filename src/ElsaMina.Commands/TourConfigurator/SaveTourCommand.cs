using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.TourConfigurator;

[NamedCommand("savetour")]
public class SaveTourCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly ITourConfigService _tourConfigService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public SaveTourCommand(IRoomsManager roomsManager,
        ITourConfigService tourConfigService,
        ITemplatesManager templatesManager,
        IConfiguration configuration)
    {
        _roomsManager = roomsManager;
        _tourConfigService = tourConfigService;
        _templatesManager = templatesManager;
        _configuration = configuration;
    }

    public override Rank RequiredRank => Rank.Driver;
    public override bool IsAllowedInPrivateMessage => true;
    public override bool IsPrivateMessageOnly => true;

    // Format : roomId;;id;;tier;;...
    public override string GetTargetRoomId(IContext context) => context.Target.Split(";;")[0].Trim();

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            context.ReplyLocalizedMessage("tourconfig_missing_data");
            return;
        }

        // Format: roomId;;id;;tier;;format;;autostart;;autodq;;tourname;;rules
        var parts = context.Target.Split(";;");
        if (parts.Length < 8)
        {
            context.ReplyLocalizedMessage("tourconfig_invalid_format");
            return;
        }

        var roomId = GetTargetRoomId(context);
        var tourId = parts[1].Trim().ToLower().Replace(" ", "");
        var tier = parts[2].Trim();
        var format = parts[3].Trim();
        var autostartStr = parts[4].Trim();
        var autodqStr = parts[5].Trim();
        var tourName = parts[6].Trim();
        var rules = string.Join(";;", parts[7..]).Trim();

        if (string.IsNullOrEmpty(tourId))
        {
            context.ReplyLocalizedMessage("tourconfig_id_required");
            return;
        }

        if (string.IsNullOrEmpty(tier))
        {
            context.ReplyLocalizedMessage("tourconfig_tier_required");
            return;
        }

        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            context.ReplyLocalizedMessage("tourconfig_room_not_found");
            return;
        }

        if (!int.TryParse(autostartStr, out var autostart))
        {
            autostart = 10;
        }

        int? autodq = int.TryParse(autodqStr, out var autodqValue) ? autodqValue : null;

        if (string.IsNullOrWhiteSpace(format))
        {
            format = "elim";
        }

        var tourConfig = new DataAccess.Models.TourConfig
        {
            Id = tourId,
            RoomId = roomId,
            Tier = tier,
            Format = format,
            Autostart = autostart,
            AutoDq = autodq,
            TourName = tourName,
            Rules = rules
        };

        await _tourConfigService.SaveTourConfigAsync(tourConfig, cancellationToken);
        context.ReplyLocalizedMessage("tourconfig_saved", tourId);

        await RefreshDashboard(context, roomId, cancellationToken);
    }

    private async Task RefreshDashboard(IContext context, string roomId, CancellationToken cancellationToken)
    {
        var configs = await _tourConfigService.GetTourConfigsForRoomAsync(roomId, cancellationToken);
        var viewModel = new TourConfigDashboardViewModel
        {
            Culture = context.Culture,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = roomId,
            TourConfigs = configs
        };
        var template = await _templatesManager.GetTemplateAsync("TourConfigurator/TourConfigDashboard", viewModel);
        context.ReplyHtmlPage($"{roomId}tourconfig", template.RemoveNewlines());
    }
}
