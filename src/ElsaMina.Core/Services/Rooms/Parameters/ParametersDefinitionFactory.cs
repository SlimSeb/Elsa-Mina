using System.Globalization;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.EventAnnounces;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Services.Rooms.Parameters;

public class ParametersDefinitionFactory : IParametersDefinitionFactory
{
    private readonly IConfiguration _configuration;
    private readonly IResourcesService _resourcesService;
    private IReadOnlyDictionary<Parameter, IParameterDefinition> _cachedDefinitions;

    public ParametersDefinitionFactory(IConfiguration configuration,
        IResourcesService resourcesService)
    {
        _configuration = configuration;
        _resourcesService = resourcesService;
    }

    public IReadOnlyDictionary<Parameter, IParameterDefinition> GetParametersDefinitions() =>
        _cachedDefinitions ??= BuildParametersDefinitions();

    private Dictionary<Parameter, IParameterDefinition> BuildParametersDefinitions() =>
        new()
        {
            [Parameter.Locale] = new ParameterDefinition
            {
                Identifier = "loc",
                NameKey = "parameter_name_locale",
                DescriptionKey = "parameter_description_locale",
                Type = RoomBotConfigurationType.Enumeration,
                DefaultValue = _configuration.DefaultLocaleCode,
                PossibleValues = _resourcesService.SupportedCultures.Select(culture => new EnumerationValue
                {
                    DisplayedValue = culture.NativeName.Capitalize(),
                    InternalValue = culture.Name
                }),
                OnUpdateAction = (room, newValue) => room.Culture = new CultureInfo(newValue)
            },
            [Parameter.TimeZone] = new ParameterDefinition
            {
                Identifier = "tzn",
                NameKey = "parameter_name_timezone",
                DescriptionKey = "parameter_description_timezone",
                Type = RoomBotConfigurationType.Enumeration,
                DefaultValue = TimeZoneInfo.Local.Id,
                PossibleValues = TimeZoneInfo.GetSystemTimeZones().Select(tz =>
                    new EnumerationValue
                    {
                        DisplayedValue = tz.DisplayName,
                        InternalValue = tz.Id
                    }),
                OnUpdateAction = (room, newValue) => room.TimeZone = TimeZoneInfo.FindSystemTimeZoneById(newValue)
            },
            [Parameter.HasCommandAutoCorrect] = new ParameterDefinition
            {
                Identifier = "atc",
                NameKey = "parameter_name_has_command_auto_correct",
                DescriptionKey = "parameter_description_has_command_auto_correct",
                Type = RoomBotConfigurationType.Boolean,
                DefaultValue = true.ToString()
            },
            [Parameter.ShowErrorMessages] = new ParameterDefinition
            {
                Identifier = "err",
                NameKey = "parameter_name_is_showing_error_messages",
                DescriptionKey = "parameter_description_is_showing_error_messages",
                Type = RoomBotConfigurationType.Boolean,
                DefaultValue = true.ToString()
            },
            [Parameter.ShowTeamLinksPreview] = new ParameterDefinition
            {
                Identifier = "tms",
                NameKey = "parameter_name_is_showing_team_links_preview",
                DescriptionKey = "parameter_description_is_showing_team_links_preview",
                Type = RoomBotConfigurationType.Boolean,
                DefaultValue = true.ToString()
            },
            [Parameter.ShowReplaysPreview] = new ParameterDefinition
            {
                Identifier = "rpl",
                NameKey = "parameter_name_is_showing_replays_preview",
                DescriptionKey = "parameter_description_is_showing_replays_preview",
                Type = RoomBotConfigurationType.Boolean,
                DefaultValue = true.ToString()
            },
            [Parameter.TournamentBettingEnabled] = new ParameterDefinition
            {
                Identifier = "tbe",
                NameKey = "parameter_name_tournament_betting_enabled",
                DescriptionKey = "parameter_description_tournament_betting_enabled",
                Type = RoomBotConfigurationType.Boolean,
                DefaultValue = true.ToString()
            },
            [Parameter.ShowYoutubeLinkPreview] = new ParameterDefinition
            {
                Identifier = "ytl",
                NameKey = "parameter_name_is_showing_youtube_link_preview",
                DescriptionKey = "parameter_description_is_showing_youtube_link_preview",
                Type = RoomBotConfigurationType.Boolean,
                DefaultValue = true.ToString()
            },
            [Parameter.ShowUrlPreview] = new ParameterDefinition
            {
                Identifier = "urlp",
                NameKey = "parameter_name_is_showing_url_preview",
                DescriptionKey = "parameter_description_is_showing_url_preview",
                Type = RoomBotConfigurationType.Boolean,
                DefaultValue = false.ToString()
            },
            [Parameter.TenorGifEnabled] = new ParameterDefinition
            {
                Identifier = "tgf",
                NameKey = "parameter_name_tenor_gif_enabled",
                DescriptionKey = "parameter_description_tenor_gif_enabled",
                Type = RoomBotConfigurationType.Boolean,
                DefaultValue = true.ToString()
            },
            [Parameter.BucksEnabled] = new ParameterDefinition
            {
                Identifier = "bck",
                NameKey = "parameter_name_bucks_enabled",
                DescriptionKey = "parameter_description_bucks_enabled",
                Type = RoomBotConfigurationType.Boolean,
                DefaultValue = false.ToString()
            },
            [Parameter.EventAnnouncesType] = new ParameterDefinition
            {
                Identifier = "evn",
                NameKey = "parameter_name_event_announces_type",
                DescriptionKey = "parameter_description_event_announces_type",
                Type = RoomBotConfigurationType.Enumeration,
                DefaultValue = EventAnnouncesTypeValues.TournamentsOnly,
                PossibleValues =
                [
                    new EnumerationValue
                    {
                        InternalValue = EventAnnouncesTypeValues.All,
                        DisplayedValue = GetDefaultLocaleString("parameter_value_event_announces_all")
                    },
                    new EnumerationValue
                    {
                        InternalValue = EventAnnouncesTypeValues.TournamentsOnly,
                        DisplayedValue = GetDefaultLocaleString("parameter_value_event_announces_tournaments")
                    },
                    new EnumerationValue
                    {
                        InternalValue = EventAnnouncesTypeValues.GamesOnly,
                        DisplayedValue = GetDefaultLocaleString("parameter_value_event_announces_games")
                    },
                    new EnumerationValue
                    {
                        InternalValue = EventAnnouncesTypeValues.None,
                        DisplayedValue = GetDefaultLocaleString("parameter_value_event_announces_none")
                    }
                ]
            }
        };

    private string GetDefaultLocaleString(string key) =>
        _resourcesService.GetString(key, new CultureInfo(_configuration.DefaultLocaleCode));
}