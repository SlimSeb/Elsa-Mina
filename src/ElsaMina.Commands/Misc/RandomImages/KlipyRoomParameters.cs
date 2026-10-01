using ElsaMina.Core.Services.Rooms.Parameters;

namespace ElsaMina.Commands.Misc.RandomImages;

public class KlipyRoomParameters : IRoomParameterProvider
{
    public static readonly Parameter KlipyGifEnabled = new("tgf", nameof(KlipyGifEnabled));

    public IEnumerable<IParameterDefinition> GetDefinitions() =>
    [
        new ParameterDefinition
        {
            Identifier = KlipyGifEnabled.Identifier,
            Name = KlipyGifEnabled.Name,
            NameKey = "parameter_name_klipy_gif_enabled",
            DescriptionKey = "parameter_description_klipy_gif_enabled",
            Type = RoomBotConfigurationType.Boolean,
            DefaultValue = true.ToString()
        }
    ];
}
