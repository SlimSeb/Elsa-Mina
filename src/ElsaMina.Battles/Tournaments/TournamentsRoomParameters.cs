using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Battles.Tournaments;

public class TournamentsRoomParameters : IRoomParameterProvider
{
    public static readonly Parameter AutoJoinTournaments = new("ajt", nameof(AutoJoinTournaments));

    public IEnumerable<IParameterDefinition> GetDefinitions() =>
    [
        new ParameterDefinition
        {
            Identifier = AutoJoinTournaments.Identifier,
            Name = AutoJoinTournaments.Name,
            NameKey = "parameter_name_auto_join_tournaments",
            DescriptionKey = "parameter_description_auto_join_tournaments",
            Type = RoomBotConfigurationType.Boolean,
            DefaultValue = false.ToString()
        }
    ];
}
