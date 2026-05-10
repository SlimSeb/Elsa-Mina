namespace ElsaMina.Commands.Misc.Food;

public interface ISpoonacularService
{
    Task<string> GetRandomRecipeHtmlAsync(string tag = null, CancellationToken cancellationToken = default);
    Task<string> SearchRecipeHtmlAsync(string query, CancellationToken cancellationToken = default);
}
