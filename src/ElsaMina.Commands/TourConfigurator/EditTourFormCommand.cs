using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.TourConfigurator;

[NamedCommand("edittourform")]
public class EditTourFormCommand : Command
{
    private readonly IRoomsManager _roomsManager;
    private readonly ITourConfigService _tourConfigService;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;

    public EditTourFormCommand(IRoomsManager roomsManager,
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

    // Format : tourId, roomId
    public override string GetTargetRoomId(IContext context)
    {
        var args = context.Target.Split(',');
        return args.Length > 1 ? args[1].Trim() : context.RoomId;
    }

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            return;
        }

        var args = context.Target.Split(',');
        var tourId = args[0].Trim();
        var roomId = GetTargetRoomId(context);

        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            context.ReplyLocalizedMessage("tourconfig_room_not_found");
            return;
        }

        var config = await _tourConfigService.GetTourConfigAsync(tourId, roomId, cancellationToken);
        if (config == null)
        {
            context.ReplyLocalizedMessage("tourconfig_not_found", tourId);
            return;
        }

        var viewModel = new TourConfigFormViewModel
        {
            Culture = context.Culture,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = roomId,
            ExistingConfig = config
        };

        var template = await _templatesManager.GetTemplateAsync("TourConfigurator/TourConfigEditForm", viewModel);
        context.ReplyHtmlPage($"{roomId}tourconfig", template.RemoveNewlines());
    }
}
