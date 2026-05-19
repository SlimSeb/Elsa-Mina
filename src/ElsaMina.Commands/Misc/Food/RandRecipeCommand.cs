using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("randrecipe")]
public class RandRecipeCommand : Command
{
    private readonly ISpoonacularService _spoonacularService;

    public RandRecipeCommand(ISpoonacularService spoonacularService)
    {
        _spoonacularService = spoonacularService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override string HelpMessageKey => "randrecipe_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await _spoonacularService.GetRandomRecipeHtmlAsync(cancellationToken: cancellationToken);
            if (html == null)
            {
                context.ReplyLocalizedMessage("spoonacular_no_results");
                return;
            }

            context.ReplyHtml(html, rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch random recipe.");
            context.ReplyLocalizedMessage("spoonacular_error");
        }
    }
}
