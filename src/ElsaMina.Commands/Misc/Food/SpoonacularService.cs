using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Misc.Food;

public class SpoonacularService : ISpoonacularService
{
    private const string RANDOM_RECIPE_URL = "https://api.spoonacular.com/recipes/random";
    private const string SEARCH_RECIPE_URL = "https://api.spoonacular.com/recipes/complexSearch";

    private readonly IHttpService _httpService;
    private readonly IConfiguration _configuration;

    public SpoonacularService(IHttpService httpService, IConfiguration configuration)
    {
        _httpService = httpService;
        _configuration = configuration;
    }

    public async Task<string> GetRandomRecipeHtmlAsync(string tag = null, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration.SpoonacularApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Spoonacular API key is empty.");
            return null;
        }

        var queryParams = new Dictionary<string, string>
        {
            ["apiKey"] = apiKey,
            ["number"] = "1"
        };
        if (!string.IsNullOrWhiteSpace(tag))
        {
            queryParams["tags"] = tag;
        }

        var response = await _httpService.GetAsync<SpoonacularRandomResponse>(RANDOM_RECIPE_URL, queryParams,
            cancellationToken: cancellationToken);
        var recipe = response.Data?.Recipes?.FirstOrDefault();
        if (recipe == null)
        {
            return null;
        }

        return await BuildRecipeHtmlAsync(recipe, apiKey, cancellationToken);
    }

    public async Task<string> SearchRecipeHtmlAsync(string query, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration.SpoonacularApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Log.Error("Spoonacular API key is empty.");
            return null;
        }

        var queryParams = new Dictionary<string, string>
        {
            ["apiKey"] = apiKey,
            ["query"] = query,
            ["number"] = "1"
        };

        var response = await _httpService.GetAsync<SpoonacularSearchResponse>(SEARCH_RECIPE_URL, queryParams,
            cancellationToken: cancellationToken);
        var recipe = response.Data?.Results?.FirstOrDefault();
        if (recipe == null)
        {
            return null;
        }

        return await BuildRecipeHtmlAsync(recipe, apiKey, cancellationToken);
    }

    private async Task<string> BuildRecipeHtmlAsync(SpoonacularRecipe recipe, string apiKey,
        CancellationToken cancellationToken)
    {
        var recipeUrl =
            $"https://spoonacular.com/recipes/{FormatRecipeTitle(recipe.Title)}-{recipe.Id}";

        var ingredients = await GetIngredientsHtmlAsync(recipe.Id, apiKey, cancellationToken);
        var instructions = await GetInstructionsHtmlAsync(recipe.Id, apiKey, cancellationToken);

        return $"""
                <div style="display: flex;">
                <div style="margin-right: 10px;">
                <a href="{recipeUrl}"><img height="100" width="100" src="{recipe.Image}" alt="{recipe.Title}"></a>
                </div>
                <div>
                <b><a href="{recipeUrl}">{recipe.Title}</a></b>
                <br><br><details>
                <summary>Ingredients</summary>
                {ingredients}
                </details><br>
                <details>
                <summary>Recipe</summary>
                <ol>
                {instructions}
                </ol>
                </details>
                </div>
                </div>
                """;
    }

    private async Task<string> GetIngredientsHtmlAsync(int recipeId, string apiKey,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.spoonacular.com/recipes/{recipeId}/ingredientWidget.json";
        var queryParams = new Dictionary<string, string> { ["apiKey"] = apiKey };

        var response =
            await _httpService.GetAsync<SpoonacularIngredientResponse>(url, queryParams,
                cancellationToken: cancellationToken);
        var ingredients = response.Data?.Ingredients;
        if (ingredients == null || ingredients.Count == 0)
        {
            return "<ul><li>No ingredients found</li></ul>";
        }

        var items = string.Join("", ingredients.Select(ingredient =>
            $"<li>{char.ToUpper(ingredient.Name[0]) + ingredient.Name[1..]}</li>"));
        return $"<ul>{items}</ul>";
    }

    private async Task<string> GetInstructionsHtmlAsync(int recipeId, string apiKey,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.spoonacular.com/recipes/{recipeId}/analyzedInstructions";
        var queryParams = new Dictionary<string, string> { ["apiKey"] = apiKey };

        var response =
            await _httpService.GetAsync<List<SpoonacularAnalyzedInstruction>>(url, queryParams,
                cancellationToken: cancellationToken);
        var steps = response.Data?.FirstOrDefault()?.Steps;
        if (steps == null || steps.Count == 0)
        {
            return "<li>No instructions found for this recipe</li>";
        }

        return string.Join("\n", steps.Select(step => $"<li>{step.Step}</li>"));
    }

    private static string FormatRecipeTitle(string title) =>
        string.Join("-", title.ToLowerInvariant().Split(' '));
}
