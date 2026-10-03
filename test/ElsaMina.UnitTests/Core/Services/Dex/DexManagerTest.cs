using ElsaMina.Core.Services.Dex;
using ElsaMina.Core.Services.Http;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Core.Services.Dex;

public class DexManagerTest
{
    private IHttpService _httpService;
    private DexManager _dexManager;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _dexManager = new DexManager(_httpService);
    }

    [Test]
    public void Test_Pokedex_ShouldBeEmpty_Initially()
    {
        Assert.That(_dexManager.Pokedex, Is.Empty);
    }

    [Test]
    public void Test_Moves_ShouldBeEmpty_Initially()
    {
        Assert.That(_dexManager.Moves, Is.Empty);
    }

    [Test]
    public async Task Test_LoadDexAsync_ShouldPopulatePokedex_WhenHttpSucceeds()
    {
        // Arrange
        var expected = new Pokemon[] { new() { Name = new Name { English = "Bulbasaur" } } };
        _httpService.SendAsync<Pokemon[]>(Arg.Any<HttpRequest>())
            .Returns(new HttpResponse<Pokemon[]> { Data = expected });

        // Act
        await _dexManager.LoadDexAsync();

        // Assert
        Assert.That(_dexManager.Pokedex, Is.EquivalentTo(expected));
    }

    [Test]
    public async Task Test_LoadDexAsync_ShouldNotThrow_WhenHttpFails()
    {
        // Arrange
        _httpService.SendAsync<Pokemon[]>(Arg.Any<HttpRequest>())
            .Throws(new Exception("Network error"));

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _dexManager.LoadDexAsync());
    }

    [Test]
    public async Task Test_LoadDexAsync_ShouldLeavePokedexEmpty_WhenHttpFails()
    {
        // Arrange
        _httpService.SendAsync<Pokemon[]>(Arg.Any<HttpRequest>())
            .Throws(new Exception("Network error"));

        // Act
        await _dexManager.LoadDexAsync();

        // Assert
        Assert.That(_dexManager.Pokedex, Is.Empty);
    }
}
