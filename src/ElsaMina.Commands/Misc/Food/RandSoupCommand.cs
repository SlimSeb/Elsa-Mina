using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.Food;

[NamedCommand("randsoup")]
public class RandSoupCommand : Command
{
    private readonly ISpoonacularService _spoonacularService;

    public RandSoupCommand(ISpoonacularService spoonacularService)
    {
        _spoonacularService = spoonacularService;
    }

    public override Rank RequiredRank => Rank.Regular;

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await _spoonacularService.GetRandomRecipeHtmlAsync("soup", cancellationToken);
            if (html == null)
            {
                context.ReplyLocalizedMessage("spoonacular_no_results");
                return;
            }

            context.ReplyHtml(html, rankAware: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch random soup recipe.");
            context.ReplyLocalizedMessage("spoonacular_error");
        }
    }
}
