using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("recipe")]
public class RecipeSearchCommand : Command
{
    private readonly ISpoonacularService _spoonacularService;

    public RecipeSearchCommand(ISpoonacularService spoonacularService)
    {
        _spoonacularService = spoonacularService;
    }

    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Target))
        {
            context.ReplyLocalizedMessage("recipe_missing_query");
            return;
        }

        try
        {
            var html = await _spoonacularService.SearchRecipeHtmlAsync(context.Target, cancellationToken);
            if (html == null)
            {
                context.ReplyLocalizedMessage("spoonacular_no_results");
                return;
            }

            context.ReplyHtml(html, rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to search recipe.");
            context.ReplyLocalizedMessage("spoonacular_error");
        }
    }
}
