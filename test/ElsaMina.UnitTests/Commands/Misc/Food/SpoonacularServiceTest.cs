using ElsaMina.Commands;
using ElsaMina.Commands.Misc.Food;
using ElsaMina.Core.Services.Http;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.Food;

public class SpoonacularServiceTest
{
    private IHttpService _httpService;
    private ICommandsConfiguration _configuration;
    private SpoonacularService _service;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<ICommandsConfiguration>();
        _configuration.SpoonacularApiKey.Returns("key");
        _service = new SpoonacularService(_httpService, _configuration);
    }

    private static IHttpResponse<T> Response<T>(T data)
    {
        return new HttpResponse<T> { Data = data };
    }

    private static SpoonacularRecipe CreateRecipe()
    {
        return new SpoonacularRecipe { Id = 42, Title = "Tomato Soup", Image = "https://img/soup.jpg" };
    }

    private void SetUpRecipeDetails(List<SpoonacularIngredient> ingredients, List<SpoonacularStep> steps)
    {
        _httpService.SendAsync<SpoonacularIngredientResponse>(
                Arg.Is<HttpRequest>(request => request.Uri.EndsWith("/42/ingredientWidget.json")),
                Arg.Any<CancellationToken>())
            .Returns(Response(new SpoonacularIngredientResponse { Ingredients = ingredients }));
        _httpService.SendAsync<List<SpoonacularAnalyzedInstruction>>(
                Arg.Is<HttpRequest>(request => request.Uri.EndsWith("/42/analyzedInstructions")),
                Arg.Any<CancellationToken>())
            .Returns(Response(new List<SpoonacularAnalyzedInstruction> { new() { Steps = steps } }));
    }

    [TestCase(null)]
    [TestCase(" ")]
    public async Task Test_GetRandomRecipeHtmlAsync_ShouldReturnNull_WhenApiKeyIsMissing(string apiKey)
    {
        // Arrange
        _configuration.SpoonacularApiKey.Returns(apiKey);

        // Act
        var html = await _service.GetRandomRecipeHtmlAsync();

        // Assert
        Assert.That(html, Is.Null);
        await _httpService.DidNotReceiveWithAnyArgs().SendAsync<SpoonacularRandomResponse>(default);
    }

    [Test]
    public async Task Test_GetRandomRecipeHtmlAsync_ShouldReturnNull_WhenNoRecipeIsFound()
    {
        // Arrange
        _httpService.SendAsync<SpoonacularRandomResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(Response(new SpoonacularRandomResponse { Recipes = [] }));

        // Act
        var html = await _service.GetRandomRecipeHtmlAsync();

        // Assert
        Assert.That(html, Is.Null);
    }

    [Test]
    public async Task Test_GetRandomRecipeHtmlAsync_ShouldBuildRecipeHtml_WhenRecipeIsFound()
    {
        // Arrange
        HttpRequest randomRequest = null;
        _httpService.SendAsync<SpoonacularRandomResponse>(Arg.Do<HttpRequest>(request => randomRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(Response(new SpoonacularRandomResponse { Recipes = [CreateRecipe()] }));
        SetUpRecipeDetails([new SpoonacularIngredient { Name = "tomato" }, new SpoonacularIngredient { Name = "salt" }],
            [new SpoonacularStep { Step = "Cut the tomatoes." }, new SpoonacularStep { Step = "Boil." }]);

        // Act
        var html = await _service.GetRandomRecipeHtmlAsync("soup");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(randomRequest.Uri, Is.EqualTo("https://api.spoonacular.com/recipes/random"));
            Assert.That(randomRequest.QueryParameters["apiKey"], Is.EqualTo("key"));
            Assert.That(randomRequest.QueryParameters["number"], Is.EqualTo("1"));
            Assert.That(randomRequest.QueryParameters["tags"], Is.EqualTo("soup"));
            Assert.That(html, Does.Contain("https://spoonacular.com/recipes/tomato-soup-42"));
            Assert.That(html, Does.Contain("src=\"https://img/soup.jpg\""));
            Assert.That(html, Does.Contain("<ul><li>Tomato</li><li>Salt</li></ul>"));
            Assert.That(html, Does.Contain("<li>Cut the tomatoes.</li>\n<li>Boil.</li>"));
        }
    }

    [Test]
    public async Task Test_GetRandomRecipeHtmlAsync_ShouldNotSendTags_WhenNoTagIsGiven()
    {
        // Arrange
        HttpRequest randomRequest = null;
        _httpService.SendAsync<SpoonacularRandomResponse>(Arg.Do<HttpRequest>(request => randomRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(Response(new SpoonacularRandomResponse()));

        // Act
        await _service.GetRandomRecipeHtmlAsync();

        // Assert
        Assert.That(randomRequest.QueryParameters.ContainsKey("tags"), Is.False);
    }

    [Test]
    public async Task Test_SearchRecipeHtmlAsync_ShouldShowPlaceholders_WhenRecipeHasNoDetails()
    {
        // Arrange
        HttpRequest searchRequest = null;
        _httpService.SendAsync<SpoonacularSearchResponse>(Arg.Do<HttpRequest>(request => searchRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(Response(new SpoonacularSearchResponse { Results = [CreateRecipe()] }));
        SetUpRecipeDetails([], []);

        // Act
        var html = await _service.SearchRecipeHtmlAsync("tomato");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(searchRequest.Uri, Is.EqualTo("https://api.spoonacular.com/recipes/complexSearch"));
            Assert.That(searchRequest.QueryParameters["query"], Is.EqualTo("tomato"));
            Assert.That(html, Does.Contain("<ul><li>No ingredients found</li></ul>"));
            Assert.That(html, Does.Contain("<li>No instructions found for this recipe</li>"));
        }
    }

    [Test]
    public async Task Test_SearchRecipeHtmlAsync_ShouldReturnNull_WhenNoResult()
    {
        // Arrange
        _httpService.SendAsync<SpoonacularSearchResponse>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(Response<SpoonacularSearchResponse>(null));

        // Act
        var html = await _service.SearchRecipeHtmlAsync("nothing");

        // Assert
        Assert.That(html, Is.Null);
    }

    [Test]
    public async Task Test_SearchRecipeHtmlAsync_ShouldReturnNull_WhenApiKeyIsMissing()
    {
        // Arrange
        _configuration.SpoonacularApiKey.Returns("");

        // Act
        var html = await _service.SearchRecipeHtmlAsync("tomato");

        // Assert
        Assert.That(html, Is.Null);
    }
}
