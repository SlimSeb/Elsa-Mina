using System.Text;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.RoomDashboard;

[NamedCommand("room-dashboard", "roomdashboard", "rdash")]
public class ShowRoomDashboard : Command
{
    private readonly IConfiguration _configuration;
    private readonly IRoomsManager _roomsManager;
    private readonly ITemplatesManager _templatesManager;
    private readonly IParametersDefinitionFactory _parametersDefinitionFactory;

    public ShowRoomDashboard(IConfiguration configuration,
        IRoomsManager roomsManager,
        ITemplatesManager templatesManager,
        IParametersDefinitionFactory parametersDefinitionFactory)
    {
        _configuration = configuration;
        _roomsManager = roomsManager;
        _templatesManager = templatesManager;
        _parametersDefinitionFactory = parametersDefinitionFactory;
    }

    public override bool IsPrivateMessageOnly => true;
    public override bool IsWhitelistOnly => true; // todo : seul un mec authed sur la room peut

    // TODO : à revoir
    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var roomId = context.Target.Trim().ToLower();
        if (string.IsNullOrEmpty(roomId))
        {
            roomId = context.RoomId;
        }

        var room = _roomsManager.GetRoom(roomId);

        if (room == null)
        {
            context.ReplyLocalizedMessage("dashboard_room_doesnt_exist", roomId);
            return;
        }

        if (context.IsPrivateMessage)
        {
            context.Culture = room.Culture;
        }

        var configurationCommandBuilder = new StringBuilder("/w ");
        configurationCommandBuilder.Append(_configuration.Name);
        configurationCommandBuilder.Append(',');
        configurationCommandBuilder.Append(_configuration.Trigger);
        configurationCommandBuilder.Append("rc ");
        configurationCommandBuilder.Append(roomId);
        configurationCommandBuilder.Append(',');
        var roomParameters = _parametersDefinitionFactory.GetParametersDefinitions();
        configurationCommandBuilder.AppendJoin(',', roomParameters
            .Values
            .Select(parameter => $"{parameter.Identifier}={{{parameter.Identifier}}}"));

        var roomParameterLines = await Task.WhenAll(roomParameters
            .Select(async kvp => new RoomParameterLineModel
            {
                Culture = context.Culture,
                RoomParameterDefinition = kvp.Value,
                CurrentValue = await room.GetParameterValueAsync(kvp.Key, cancellationToken)
            }));

        var viewModel = new RoomDashboardViewModel
        {
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = roomId,
            Command = configurationCommandBuilder.ToString(),
            RoomName = room.Name,
            Culture = context.Culture,
            RoomParameterLines = roomParameterLines
        };
        var template = await _templatesManager.GetTemplateAsync("RoomDashboard/RoomDashboard", viewModel);

        context.ReplyHtmlPage($"{roomId}dashboard", template.RemoveNewlines());
    }
}
