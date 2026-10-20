using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.TourConfigurator;

[NamedCommand("configtour", Aliases = ["tourconfig"])]
public class ConfigTourCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly ITourConfigService _tourConfigService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public ConfigTourCommand(IRoomsManager roomsManager,
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

    public override string GetTargetRoomId(IContext context) =>
        string.IsNullOrWhiteSpace(context.Target) ? context.RoomId : context.Target.Trim().ToLower();

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = GetTargetRoomId(context);

        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            context.ReplyLocalizedMessage("tourconfig_room_not_found");
            return;
        }

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
