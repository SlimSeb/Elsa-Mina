using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Commands.Tournaments.Betting;

public class TournamentBettingRoomParameters : IRoomParameterProvider
{
    public static readonly Parameter TournamentBettingEnabled = new("tbe", nameof(TournamentBettingEnabled));

    public IEnumerable<IParameterDefinition> GetDefinitions() =>
    [
        new ParameterDefinition
        {
            Identifier = TournamentBettingEnabled.Identifier,
            Name = TournamentBettingEnabled.Name,
            NameKey = "parameter_name_tournament_betting_enabled",
            DescriptionKey = "parameter_description_tournament_betting_enabled",
            Type = RoomBotConfigurationType.Boolean,
            DefaultValue = true.ToString()
        }
    ];
}
