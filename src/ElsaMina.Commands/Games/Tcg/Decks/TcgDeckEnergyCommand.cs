using ElsaMina.Commands.Games.Tcg.Cards;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Games.Tcg.Decks;

[NamedCommand("tcgdeckenergy", Aliases = ["tcgenergy"])]
public class TcgDeckEnergyCommand : TcgDeckCommandBase
{
    public TcgDeckEnergyCommand(ITcgDeckService deckService, ITemplatesManager templatesManager,
        IConfiguration configuration) : base(deckService, templatesManager, configuration)
    {
    }

    public override string HelpMessageKey => "tcg_deckenergy_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var parts = (context.Target ?? string.Empty).Split(',', 2);
        if (parts.Length < 2)
        {
            context.ReplyLocalizedMessage("tcg_deckenergy_usage", Configuration.Trigger);
            return;
        }

        var deckName = parts[0].Trim();
        var energyTypes = new List<TcgType>();
        foreach (var token in parts[1].Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TcgTypes.TryParse(token, out var type))
            {
                context.ReplyLocalizedMessage("tcg_deckenergy_unknown_type", token);
                return;
            }

            energyTypes.Add(type);
        }

        var result = await DeckService.SetEnergyTypesAsync(context.Sender.UserId, deckName, energyTypes,
            cancellationToken);
        await ApplyResultAsync(context, result);
    }
}
