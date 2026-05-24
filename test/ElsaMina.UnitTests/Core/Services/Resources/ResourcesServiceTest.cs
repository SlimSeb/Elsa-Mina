using System.Collections;
using System.Globalization;
using System.Resources;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Resources;

public class ResourcesServiceTest
{
    private IConfiguration _configuration;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _configuration.DefaultLocaleCode.Returns("en-US");
    }

    private ResourcesService CreateService(params ResourceManager[] managers) =>
        new(_configuration, managers);

    [Test]
    public void Test_GetString_ShouldReturnLocalizedValue_WhenKeyExistsInRequestedCulture()
    {
        // Arrange
        var manager = FakeResourceManager.For(new CultureInfo("en-US"), new Dictionary<string, string>
        {
            ["hello"] = "Hello"
        });
        var sut = CreateService(manager);

        // Act
        var result = sut.GetString("hello", new CultureInfo("en-US"));

        // Assert
        Assert.That(result, Is.EqualTo("Hello"));
    }

    [Test]
    public void Test_GetString_ShouldReturnKey_WhenKeyNotFoundInAnyManager()
    {
        // Arrange
        var manager = FakeResourceManager.For(new CultureInfo("en-US"), new Dictionary<string, string>
        {
            ["other"] = "Other"
        });
        var sut = CreateService(manager);

        // Act
        var result = sut.GetString("missing_key", new CultureInfo("en-US"));

        // Assert
        Assert.That(result, Is.EqualTo("missing_key"));
    }

    [Test]
    public void Test_GetString_ShouldUseDefaultCulture_WhenNoCultureProvided()
    {
        // Arrange
        _configuration.DefaultLocaleCode.Returns("fr-FR");
        var manager = FakeResourceManager.For(new CultureInfo("fr-FR"), new Dictionary<string, string>
        {
            ["hello"] = "Bonjour"
        });
        var sut = CreateService(manager);

        // Act
        var result = sut.GetString("hello");

        // Assert
        Assert.That(result, Is.EqualTo("Bonjour"));
    }

    [Test]
    public void Test_GetString_ShouldFallBackToNeutralCulture_WhenSpecificCultureMissing()
    {
        // Arrange - only the neutral "fr" culture has the key, not "fr-FR"
        var manager = FakeResourceManager.For(new CultureInfo("fr"), new Dictionary<string, string>
        {
            ["hello"] = "Bonjour"
        });
        var sut = CreateService(manager);

        // Act - request fr-FR, which should walk up to fr
        var result = sut.GetString("hello", new CultureInfo("fr-FR"));

        // Assert
        Assert.That(result, Is.EqualTo("Bonjour"));
    }

    [Test]
    public void Test_GetString_ShouldFallBackToInvariantCulture_WhenOnlyInvariantHasKey()
    {
        // Arrange
        var manager = FakeResourceManager.For(CultureInfo.InvariantCulture, new Dictionary<string, string>
        {
            ["hello"] = "Hello (invariant)"
        });
        var sut = CreateService(manager);

        // Act
        var result = sut.GetString("hello", new CultureInfo("en-US"));

        // Assert
        Assert.That(result, Is.EqualTo("Hello (invariant)"));
    }

    [Test]
    public void Test_GetString_ShouldReturnFirstManagerValue_WhenKeyPresentInMultipleManagers()
    {
        // Arrange
        var first = FakeResourceManager.For(new CultureInfo("en-US"), new Dictionary<string, string>
        {
            ["greeting"] = "Hello from first"
        });
        var second = FakeResourceManager.For(new CultureInfo("en-US"), new Dictionary<string, string>
        {
            ["greeting"] = "Hello from second"
        });
        var sut = CreateService(first, second);

        // Act
        var result = sut.GetString("greeting", new CultureInfo("en-US"));

        // Assert
        Assert.That(result, Is.EqualTo("Hello from first"));
    }

    [Test]
    public void Test_GetString_ShouldSearchSecondManager_WhenFirstManagerLacksKey()
    {
        // Arrange
        var first = FakeResourceManager.For(new CultureInfo("en-US"), new Dictionary<string, string>
        {
            ["other"] = "Other"
        });
        var second = FakeResourceManager.For(new CultureInfo("en-US"), new Dictionary<string, string>
        {
            ["greeting"] = "Hello from second"
        });
        var sut = CreateService(first, second);

        // Act
        var result = sut.GetString("greeting", new CultureInfo("en-US"));

        // Assert
        Assert.That(result, Is.EqualTo("Hello from second"));
    }

    [Test]
    public void Test_GetString_ShouldReturnCorrectLocale_WhenMultipleCulturesRegistered()
    {
        // Arrange
        var manager = FakeResourceManager.WithMultipleCultures(new Dictionary<CultureInfo, Dictionary<string, string>>
        {
            [new CultureInfo("en-US")] = new() { ["greet"] = "Hello" },
            [new CultureInfo("fr-FR")] = new() { ["greet"] = "Bonjour" }
        });
        var sut = CreateService(manager);

        // Act
        var enResult = sut.GetString("greet", new CultureInfo("en-US"));
        var frResult = sut.GetString("greet", new CultureInfo("fr-FR"));

        // Assert
        Assert.That(enResult, Is.EqualTo("Hello"));
        Assert.That(frResult, Is.EqualTo("Bonjour"));
    }

    [Test]
    public void Test_SupportedCultures_ShouldContainAllCulturesWithRegisteredResourceSets()
    {
        // Arrange
        var manager = FakeResourceManager.WithMultipleCultures(new Dictionary<CultureInfo, Dictionary<string, string>>
        {
            [new CultureInfo("en-US")] = new() { ["key"] = "val" },
            [new CultureInfo("fr-FR")] = new() { ["key"] = "val" }
        });
        var sut = CreateService(manager);

        // Act
        var supported = sut.SupportedCultures.Select(c => c.Name).ToHashSet();

        // Assert
        Assert.That(supported, Does.Contain("en-US"));
        Assert.That(supported, Does.Contain("fr-FR"));
    }

    [Test]
    public void Test_SupportedCultures_ShouldMergeAcrossManagers()
    {
        // Arrange
        var first = FakeResourceManager.For(new CultureInfo("en-US"), new Dictionary<string, string> { ["a"] = "a" });
        var second = FakeResourceManager.For(new CultureInfo("fr-FR"), new Dictionary<string, string> { ["b"] = "b" });
        var sut = CreateService(first, second);

        // Act
        var supported = sut.SupportedCultures.Select(c => c.Name).ToHashSet();

        // Assert
        Assert.That(supported, Does.Contain("en-US"));
        Assert.That(supported, Does.Contain("fr-FR"));
    }

    [Test]
    public void Test_GetString_ShouldReturnKey_WhenNoManagersRegistered()
    {
        // Arrange
        var sut = CreateService();

        // Act
        var result = sut.GetString("some_key", new CultureInfo("en-US"));

        // Assert
        Assert.That(result, Is.EqualTo("some_key"));
    }

    // --- Test doubles ---

    private sealed class FakeResourceManager : ResourceManager
    {
        private readonly Dictionary<string, ResourceSet> _sets;

        private FakeResourceManager(Dictionary<string, ResourceSet> sets)
        {
            _sets = sets;
        }

        public static FakeResourceManager For(CultureInfo culture, Dictionary<string, string> entries) =>
            WithMultipleCultures(new Dictionary<CultureInfo, Dictionary<string, string>> { [culture] = entries });

        public static FakeResourceManager WithMultipleCultures(
            Dictionary<CultureInfo, Dictionary<string, string>> data)
        {
            var sets = data.ToDictionary(
                kvp => kvp.Key.Name,
                kvp => new ResourceSet(new DictionaryResourceReader(kvp.Value)));
            return new FakeResourceManager(sets);
        }

        public override ResourceSet GetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents) =>
            _sets.GetValueOrDefault(culture.Name);
    }

    private sealed class DictionaryResourceReader : IResourceReader
    {
        private readonly Dictionary<string, object> _data;

        public DictionaryResourceReader(Dictionary<string, string> data) =>
            _data = data.ToDictionary(kvp => kvp.Key, kvp => (object) kvp.Value);

        public IDictionaryEnumerator GetEnumerator() => _data.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();
        public void Close() { }
        public void Dispose() { }
    }
}
