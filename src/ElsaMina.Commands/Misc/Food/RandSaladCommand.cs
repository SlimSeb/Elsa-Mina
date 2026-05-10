using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("randsalad")]
public class RandSaladCommand : Command
{
    private readonly ISpoonacularService _spoonacularService;

    public RandSaladCommand(ISpoonacularService spoonacularService)
    {
        _spoonacularService = spoonacularService;
    }

    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await _spoonacularService.GetRandomRecipeHtmlAsync("salad", cancellationToken);
            if (html == null)
            {
                context.ReplyLocalizedMessage("spoonacular_no_results");
                return;
            }

            context.ReplyHtml(html, rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch random salad recipe.");
            context.ReplyLocalizedMessage("spoonacular_error");
        }
    }
}
