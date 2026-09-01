using System.Text;
using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.RoomDashboard;

public class RoomDashboardService : IRoomDashboardService
{
    private readonly IConfiguration _configuration;
    private readonly IRoomsManager _roomsManager;
    private readonly ITemplatesManager _templatesManager;
    private readonly IParametersDefinitionFactory _parametersDefinitionFactory;
    private readonly IArcadeEventsService _arcadeEventsService;

    public RoomDashboardService(
        IConfiguration configuration,
        IRoomsManager roomsManager,
        ITemplatesManager templatesManager,
        IParametersDefinitionFactory parametersDefinitionFactory,
        IArcadeEventsService arcadeEventsService)
    {
        _configuration = configuration;
        _roomsManager = roomsManager;
        _templatesManager = templatesManager;
        _parametersDefinitionFactory = parametersDefinitionFactory;
        _arcadeEventsService = arcadeEventsService;
    }

    internal async Task<RoomDashboardViewModel> BuildViewModelAsync(
        string roomId,
        IContext context,
        CancellationToken cancellationToken = default)
    {
        var room = _roomsManager.GetRoom(roomId);
        if (room == null)
        {
            return null;
        }

        var roomParameters = _parametersDefinitionFactory.GetParametersDefinitions();

        var configurationCommandBuilder = new StringBuilder("/w ");
        configurationCommandBuilder.Append(_configuration.Name);
        configurationCommandBuilder.Append(", ");
        configurationCommandBuilder.Append(_configuration.Trigger);
        configurationCommandBuilder.Append("rc ");
        configurationCommandBuilder.Append(roomId);
        configurationCommandBuilder.Append(", ");
        configurationCommandBuilder.AppendJoin(", ", roomParameters
            .Values
            .Select(parameter => $"{parameter.Identifier}={{{parameter.Identifier}}}"));

        var lineModels = new List<RoomParameterLineModel>();
        foreach (var (parameterKey, parameterDefinition) in roomParameters)
        {
            var currentValue = await room.GetParameterValueAsync(parameterKey, cancellationToken);
            lineModels.Add(new RoomParameterLineModel
            {
                Culture = context.Culture,
                ParameterKey = parameterKey,
                RoomParameterDefinition = parameterDefinition,
                CurrentValue = currentValue,
                BotName = _configuration.Name,
                Trigger = _configuration.Trigger,
                RoomId = roomId
            });
        }

        var categories = GroupIntoCategories(lineModels, context);
        var areGamesMuted = _arcadeEventsService.AreGamesMuted(roomId);

        return new RoomDashboardViewModel
        {
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = roomId,
            RoomName = room.Name,
            Command = configurationCommandBuilder.ToString(),
            Culture = context.Culture,
            RoomParameterLines = lineModels,
            Categories = categories,
            AreGamesMuted = areGamesMuted
        };
    }

    public async Task SendDashboardPageAsync(
        IContext context,
        string roomId,
        CancellationToken cancellationToken = default)
    {
        var viewModel = await BuildViewModelAsync(roomId, context, cancellationToken);
        if (viewModel == null)
        {
            return;
        }

        var template = await _templatesManager.GetTemplateAsync("RoomDashboard/RoomDashboard", viewModel);
        context.ReplyHtmlPage($"{roomId}dashboard", template.RemoveNewlines().CollapseAttributeWhitespace());
    }

    private static IReadOnlyList<RoomDashboardCategoryViewModel> GroupIntoCategories(
        IReadOnlyList<RoomParameterLineModel> lineModels,
        IContext context)
    {
        var generalParameters = new HashSet<Parameter>
        {
            Parameter.Locale,
            Parameter.TimeZone,
            Parameter.HasCommandAutoCorrect,
            Parameter.ShowErrorMessages
        };

        var previewParameters = new HashSet<Parameter>
        {
            Parameter.ShowTeamLinksPreview,
            Parameter.ShowReplaysPreview,
            Parameter.ShowYoutubeLinkPreview,
            Parameter.ShowUrlPreview,
            Parameter.KlipyGifEnabled
        };

        var arcadeParameters = new HashSet<Parameter>
        {
            Parameter.EventAnnouncesType,
            Parameter.BucksEnabled,
            Parameter.StreaksEnabled,
            Parameter.TournamentBettingEnabled
        };

        var generalList = lineModels.Where(line => generalParameters.Contains(line.ParameterKey)).ToList();
        var previewsList = lineModels.Where(line => previewParameters.Contains(line.ParameterKey)).ToList();
        var arcadeList = lineModels.Where(line => arcadeParameters.Contains(line.ParameterKey)).ToList();
        var otherList = lineModels.Where(line => !generalParameters.Contains(line.ParameterKey) &&
                                                  !previewParameters.Contains(line.ParameterKey) &&
                                                  !arcadeParameters.Contains(line.ParameterKey)).ToList();

        if (otherList.Count > 0)
        {
            generalList.AddRange(otherList);
        }

        return
        [
            new RoomDashboardCategoryViewModel
            {
                Culture = context.Culture,
                CategoryKey = "general",
                TitleKey = "dashboard_category_general",
                Parameters = generalList
            },
            new RoomDashboardCategoryViewModel
            {
                Culture = context.Culture,
                CategoryKey = "previews",
                TitleKey = "dashboard_category_previews",
                Parameters = previewsList
            },
            new RoomDashboardCategoryViewModel
            {
                Culture = context.Culture,
                CategoryKey = "arcade",
                TitleKey = "dashboard_category_arcade",
                Parameters = arcadeList
            }
        ];
    }
}
