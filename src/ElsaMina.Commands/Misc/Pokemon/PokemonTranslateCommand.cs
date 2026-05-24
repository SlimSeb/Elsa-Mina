using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.Pokemon;

[NamedCommand("ptranslate", Aliases = ["pokemon-translate"])]
public class PokemonTranslateCommand : Command
{
    private readonly IDexManager _dexManager;

    public PokemonTranslateCommand(IDexManager dexManager)
    {
        _dexManager = dexManager;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "pokemon_translate_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            context.ReplyRankAwareLocalizedMessage("pokemon_name_required");
            return Task.CompletedTask;
        }

        var query = context.Target.Trim();
        var normalizedQuery = query.ToLowerInvariant();

        var pokemon = _dexManager.Pokedex.FirstOrDefault(p =>
            p.Name?.English?.ToLowerInvariant() == normalizedQuery ||
            p.Name?.French?.ToLowerInvariant() == normalizedQuery ||
            p.Name?.Japanese?.ToLowerInvariant() == normalizedQuery);

        if (pokemon is null)
        {
            context.ReplyRankAwareLocalizedMessage("pokemon_translate_not_found", query);
            return Task.CompletedTask;
        }

        context.Reply(
            $"**#{pokemon.PokedexId} {pokemon.Name.English}** - " +
            $"EN: {pokemon.Name.English} | FR: {pokemon.Name.French} | JP: {pokemon.Name.Japanese}",
            rankAware: true);

        return Task.CompletedTask;
    }
}