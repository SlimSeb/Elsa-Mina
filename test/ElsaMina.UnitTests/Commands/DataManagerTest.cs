using ElsaMina.Commands;
using Newtonsoft.Json;

namespace ElsaMina.UnitTests.Commands;

[TestFixture]
public class DataManagerTest
{
    private string _tempDirectory;
    private DataManager _sut;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        _sut = new DataManager(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Test_CountriesGameData_ShouldLoadData_WhenPropertyAccessed()
    {
        // Arrange
        var json = JsonConvert.SerializeObject(new
        {
            values = new[]
            {
                new { english_name = "France", french_name = "France", flag = "flag.png", location = "loc.png" }
            }
        });
        File.WriteAllText(Path.Combine(_tempDirectory, "countries_game.json"), json);

        // Act
        var result = _sut.CountriesGameData;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Countries, Is.Not.Null);
        var countriesList = result.Countries.ToList();
        Assert.That(countriesList, Has.Count.EqualTo(1));
        Assert.That(countriesList[0].EnglishName, Is.EqualTo("France"));
    }

    [Test]
    public void Test_PokemonDescriptions_ShouldLoadData_WhenPropertyAccessed()
    {
        // Arrange
        var json = JsonConvert.SerializeObject(new[]
        {
            new { EnglishName = "Pikachu", FrenchName = "Pikachu", Description = "Mouse Pokémon" }
        });
        File.WriteAllText(Path.Combine(_tempDirectory, "pokedesc.json"), json);

        // Act
        var result = _sut.PokemonDescriptions;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].EnglishName, Is.EqualTo("Pikachu"));
    }

    [Test]
    public void Test_CapitalCitiesGameData_ShouldLoadData_WhenPropertyAccessed()
    {
        // Arrange
        var json = JsonConvert.SerializeObject(new[]
        {
            new
            {
                country_en = "France",
                country_fr = "France",
                capital_en = "Paris",
                capital_fr = "Paris"
            }
        });
        File.WriteAllText(Path.Combine(_tempDirectory, "capital_cities.json"), json);

        // Act
        var result = _sut.CapitalCitiesGameData;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Capitals, Is.Not.Null);
        Assert.That(result.Capitals, Has.Count.EqualTo(1));
        Assert.That(result.Capitals[0].CapitalEnglish, Is.EqualTo("Paris"));
    }

    [Test]
    public void Test_WordleWords_ShouldLoadData_WhenPropertyAccessed()
    {
        // Arrange
        var json = JsonConvert.SerializeObject(new[] { "apple", "crane", "level" });
        File.WriteAllText(Path.Combine(_tempDirectory, "wordle_words.json"), json);

        // Act
        var result = _sut.WordleWords;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Is.EqualTo("apple"));
    }

    [Test]
    public void Test_WordleWordsFr_ShouldLoadData_WhenPropertyAccessed()
    {
        // Arrange
        var json = JsonConvert.SerializeObject(new[] { "avion", "blanc" });
        File.WriteAllText(Path.Combine(_tempDirectory, "wordle_words_fr.json"), json);

        // Act
        var result = _sut.WordleWordsFr;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Is.EqualTo("avion"));
    }

    [Test]
    public void Test_SemantixWordsFr_ShouldLoadData_WhenPropertyAccessed()
    {
        // Arrange
        var json = JsonConvert.SerializeObject(new[] { "mot", "arbre" });
        File.WriteAllText(Path.Combine(_tempDirectory, "semantix_words_fr.json"), json);

        // Act
        var result = _sut.SemantixWordsFr;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Is.EqualTo("mot"));
    }

    [Test]
    public void Test_SemantixAnswersFr_ShouldLoadData_WhenPropertyAccessed()
    {
        // Arrange
        var json = JsonConvert.SerializeObject(new[] { "soleil", "lune" });
        File.WriteAllText(Path.Combine(_tempDirectory, "semantix_answers_fr.json"), json);

        // Act
        var result = _sut.SemantixAnswersFr;

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0], Is.EqualTo("soleil"));
    }

    [Test]
    public void Test_Properties_ShouldReturnCachedInstance_OnSubsequentAccesses()
    {
        // Arrange
        var json = JsonConvert.SerializeObject(new[] { "apple", "crane" });
        var filePath = Path.Combine(_tempDirectory, "wordle_words.json");
        File.WriteAllText(filePath, json);

        // Act
        var firstAccess = _sut.WordleWords;
        File.Delete(filePath); // Delete file to ensure second access does not re-read from disk
        var secondAccess = _sut.WordleWords;

        // Assert
        Assert.That(secondAccess, Is.SameAs(firstAccess));
    }

    [Test]
    public void Test_Properties_ShouldHandleMissingFilesGracefully_WhenFilesDoNotExist()
    {
        // Act & Assert (none of these should throw unhandled exceptions)
        Assert.That(_sut.CountriesGameData.Countries, Is.Empty);
        Assert.That(_sut.PokemonDescriptions, Is.Null);
        Assert.That(_sut.CapitalCitiesGameData.Capitals, Is.Empty);
        Assert.That(_sut.WordleWords, Is.Null);
        Assert.That(_sut.WordleWordsFr, Is.Null);
        Assert.That(_sut.SemantixWordsFr, Is.Null);
        Assert.That(_sut.SemantixAnswersFr, Is.Null);
    }
}
