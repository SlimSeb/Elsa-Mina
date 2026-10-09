using System.Globalization;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Services.Rooms.Parameters;

public class CoreRoomParameters : IRoomParameterProvider
{
    private readonly IConfiguration _configuration;
    private readonly IResourcesService _resourcesService;

    public CoreRoomParameters(IConfiguration configuration, IResourcesService resourcesService)
    {
        _configuration = configuration;
        _resourcesService = resourcesService;
    }

    public IEnumerable<IParameterDefinition> GetDefinitions() =>
    [
        new ParameterDefinition
        {
            Identifier = Parameter.Locale.Identifier,
            Name = Parameter.Locale.Name,
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
        new ParameterDefinition
        {
            Identifier = Parameter.TimeZone.Identifier,
            Name = Parameter.TimeZone.Name,
            NameKey = "parameter_name_timezone",
            DescriptionKey = "parameter_description_timezone",
            Type = RoomBotConfigurationType.Enumeration,
            DefaultValue = TimeZoneInfo.Local.Id,
            PossibleValues = TimeZoneInfo.GetSystemTimeZones().Select(timeZone =>
                new EnumerationValue
                {
                    DisplayedValue = timeZone.DisplayName,
                    InternalValue = timeZone.Id
                }),
            OnUpdateAction = (room, newValue) => room.TimeZone = TimeZoneInfo.FindSystemTimeZoneById(newValue)
        },
        new ParameterDefinition
        {
            Identifier = Parameter.HasCommandAutoCorrect.Identifier,
            Name = Parameter.HasCommandAutoCorrect.Name,
            NameKey = "parameter_name_has_command_auto_correct",
            DescriptionKey = "parameter_description_has_command_auto_correct",
            Type = RoomBotConfigurationType.Boolean,
            DefaultValue = true.ToString()
        },
        new ParameterDefinition
        {
            Identifier = Parameter.ShowErrorMessages.Identifier,
            Name = Parameter.ShowErrorMessages.Name,
            NameKey = "parameter_name_is_showing_error_messages",
            DescriptionKey = "parameter_description_is_showing_error_messages",
            Type = RoomBotConfigurationType.Boolean,
            DefaultValue = true.ToString()
        }
    ];
}
