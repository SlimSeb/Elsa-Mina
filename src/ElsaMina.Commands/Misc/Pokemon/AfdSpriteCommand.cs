using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.Pokemon;

[NamedCommand("afd", "afd-back")]
public class AfdSpriteCommand : Command
{
    private const string FRONT_SPRITE_URL = "https://play.pokemonshowdown.com/sprites/afd/{0}.png";
    private const string BACK_SPRITE_URL = "https://play.pokemonshowdown.com/sprites/afd-back/{0}.png";

    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "afd_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            context.ReplyRankAwareLocalizedMessage("pokemon_name_required");
            return Task.CompletedTask;
        }

        var pokemonName = context.Target.ToLowerInvariant();
        var isBackSprite = context.Command == "afd-back";
        var spriteUrl = string.Format(isBackSprite ? BACK_SPRITE_URL : FRONT_SPRITE_URL, pokemonName);
        var spriteType = isBackSprite ? "back" : "front";

        var imgHtml = $"""<img src="{spriteUrl}" width="80" height="80" alt="{pokemonName} {spriteType} sprite">""";
        context.ReplyHtml(imgHtml, rankAware: true);

        return Task.CompletedTask;
    }
}