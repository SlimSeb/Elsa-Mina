using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Commands.EventAnnounces;

public class EventAnnouncesRoomParameters : IRoomParameterProvider
{
    public static readonly Parameter EventAnnouncesType = new("evn", nameof(EventAnnouncesType));

    public IEnumerable<IParameterDefinition> GetDefinitions() =>
    [
        new ParameterDefinition
        {
            Identifier = EventAnnouncesType.Identifier,
            Name = EventAnnouncesType.Name,
            NameKey = "parameter_name_event_announces_type",
            DescriptionKey = "parameter_description_event_announces_type",
            Type = RoomBotConfigurationType.Enumeration,
            DefaultValue = EventAnnouncesTypeValues.TournamentsOnly,
            PossibleValues =
            [
                new EnumerationValue
                {
                    InternalValue = EventAnnouncesTypeValues.All,
                    DisplayedValueKey = "parameter_value_event_announces_all"
                },
                new EnumerationValue
                {
                    InternalValue = EventAnnouncesTypeValues.TournamentsOnly,
                    DisplayedValueKey = "parameter_value_event_announces_tournaments"
                },
                new EnumerationValue
                {
                    InternalValue = EventAnnouncesTypeValues.GamesOnly,
                    DisplayedValueKey = "parameter_value_event_announces_games"
                },
                new EnumerationValue
                {
                    InternalValue = EventAnnouncesTypeValues.None,
                    DisplayedValueKey = "parameter_value_event_announces_none"
                }
            ]
        }
    ];
}
