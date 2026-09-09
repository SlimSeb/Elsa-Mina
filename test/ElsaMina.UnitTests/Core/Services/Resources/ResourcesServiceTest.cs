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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(enResult, Is.EqualTo("Hello"));
            Assert.That(frResult, Is.EqualTo("Bonjour"));
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(supported, Does.Contain("en-US"));
            Assert.That(supported, Does.Contain("fr-FR"));
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(supported, Does.Contain("en-US"));
            Assert.That(supported, Does.Contain("fr-FR"));
        }
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

    [Test]
    public void Test_Constructor_ShouldNotLoadAnyStrings_WhenInitialized()
    {
        // Arrange
        var manager = FakeResourceManager.WithMultipleCultures(new Dictionary<CultureInfo, Dictionary<string, string>>
        {
            [new CultureInfo("en-US")] = new() { ["key1"] = "val1" },
            [new CultureInfo("fr-FR")] = new() { ["key2"] = "val2" }
        });

        // Act
        _ = CreateService(manager);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(manager.GetReaderEnumerationCount("en-US"), Is.Zero);
            Assert.That(manager.GetReaderEnumerationCount("fr-FR"), Is.Zero);
        }
    }

    [Test]
    public void Test_SupportedCultures_ShouldNotLoadStrings_WhenAccessed()
    {
        // Arrange
        var manager = FakeResourceManager.WithMultipleCultures(new Dictionary<CultureInfo, Dictionary<string, string>>
        {
            [new CultureInfo("en-US")] = new() { ["key1"] = "val1" },
            [new CultureInfo("fr-FR")] = new() { ["key2"] = "val2" }
        });
        var sut = CreateService(manager);

        // Act
        _ = sut.SupportedCultures.ToList();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(manager.GetReaderEnumerationCount("en-US"), Is.Zero);
            Assert.That(manager.GetReaderEnumerationCount("fr-FR"), Is.Zero);
        }
    }

    [Test]
    public void Test_GetString_ShouldOnlyLoadRequestedCulture_AndNotUnusedCultures()
    {
        // Arrange
        var manager = FakeResourceManager.WithMultipleCultures(new Dictionary<CultureInfo, Dictionary<string, string>>
        {
            [new CultureInfo("en-US")] = new() { ["hello"] = "Hello" },
            [new CultureInfo("fr-FR")] = new() { ["hello"] = "Bonjour" },
            [new CultureInfo("de-DE")] = new() { ["hello"] = "Hallo" }
        });
        var sut = CreateService(manager);

        // Act
        var result = sut.GetString("hello", new CultureInfo("en-US"));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo("Hello"));
            Assert.That(manager.GetReaderEnumerationCount("en-US"), Is.EqualTo(1));
            Assert.That(manager.GetReaderEnumerationCount("fr-FR"), Is.Zero);
            Assert.That(manager.GetReaderEnumerationCount("de-DE"), Is.Zero);
        }
    }

    [Test]
    public void Test_GetString_ShouldCacheLoadedStrings_WhenCalledMultipleTimesForSameCulture()
    {
        // Arrange
        var manager = FakeResourceManager.WithMultipleCultures(new Dictionary<CultureInfo, Dictionary<string, string>>
        {
            [new CultureInfo("en-US")] = new() { ["hello"] = "Hello", ["world"] = "World" }
        });
        var sut = CreateService(manager);

        // Act
        var firstResult = sut.GetString("hello", new CultureInfo("en-US"));
        var secondResult = sut.GetString("world", new CultureInfo("en-US"));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstResult, Is.EqualTo("Hello"));
            Assert.That(secondResult, Is.EqualTo("World"));
            Assert.That(manager.GetReaderEnumerationCount("en-US"), Is.EqualTo(1));
        }
    }

    [Test]
    public void Test_GetString_ShouldLoadParentCultureLazily_WhenKeyNotInChildCulture()
    {
        // Arrange
        var manager = FakeResourceManager.WithMultipleCultures(new Dictionary<CultureInfo, Dictionary<string, string>>
        {
            [new CultureInfo("fr-FR")] = new() { ["other"] = "Autre" },
            [new CultureInfo("fr")] = new() { ["hello"] = "Bonjour" }
        });
        var sut = CreateService(manager);

        // Act
        var result = sut.GetString("hello", new CultureInfo("fr-FR"));

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo("Bonjour"));
            Assert.That(manager.GetReaderEnumerationCount("fr-FR"), Is.EqualTo(1));
            Assert.That(manager.GetReaderEnumerationCount("fr"), Is.EqualTo(1));
        }
    }

    [Test]
    public void Test_GetString_ShouldLoadMultipleCulturesOnDemand_WhenDifferentCulturesRequested()
    {
        // Arrange
        var manager = FakeResourceManager.WithMultipleCultures(new Dictionary<CultureInfo, Dictionary<string, string>>
        {
            [new CultureInfo("en-US")] = new() { ["hello"] = "Hello" },
            [new CultureInfo("fr-FR")] = new() { ["hello"] = "Bonjour" },
            [new CultureInfo("de-DE")] = new() { ["hello"] = "Hallo" }
        });
        var sut = CreateService(manager);

        // Act & Assert step 1: Request en-US
        var enResult = sut.GetString("hello", new CultureInfo("en-US"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(enResult, Is.EqualTo("Hello"));
            Assert.That(manager.GetReaderEnumerationCount("en-US"), Is.EqualTo(1));
            Assert.That(manager.GetReaderEnumerationCount("fr-FR"), Is.Zero);
            Assert.That(manager.GetReaderEnumerationCount("de-DE"), Is.Zero);
        }

        // Act & Assert step 2: Request fr-FR
        var frResult = sut.GetString("hello", new CultureInfo("fr-FR"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(frResult, Is.EqualTo("Bonjour"));
            Assert.That(manager.GetReaderEnumerationCount("en-US"), Is.EqualTo(1));
            Assert.That(manager.GetReaderEnumerationCount("fr-FR"), Is.EqualTo(1));
            Assert.That(manager.GetReaderEnumerationCount("de-DE"), Is.Zero);
        }
    }

    [Test]
    public async Task Test_GetString_ShouldLoadCultureOnlyOnce_WhenCalledConcurrently()
    {
        // Arrange
        var manager = FakeResourceManager.WithMultipleCultures(new Dictionary<CultureInfo, Dictionary<string, string>>
        {
            [new CultureInfo("en-US")] = new() { ["hello"] = "Hello" }
        });
        var sut = CreateService(manager);

        // Act
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(() => sut.GetString("hello", new CultureInfo("en-US"))));
        var results = await Task.WhenAll(tasks);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Has.All.EqualTo("Hello"));
            Assert.That(manager.GetReaderEnumerationCount("en-US"), Is.EqualTo(1));
        }
    }

    // --- Test doubles ---

    private sealed class FakeResourceManager : ResourceManager
    {
        private readonly Dictionary<string, TrackingResourceSet> _sets;

        private FakeResourceManager(Dictionary<string, TrackingResourceSet> sets)
        {
            _sets = sets;
        }

        public static FakeResourceManager For(CultureInfo culture, Dictionary<string, string> entries) =>
            WithMultipleCultures(new Dictionary<CultureInfo, Dictionary<string, string>> { [culture] = entries });

        public static FakeResourceManager WithMultipleCultures(
            Dictionary<CultureInfo, Dictionary<string, string>> data)
        {
            var sets = data.ToDictionary(
                pair => pair.Key.Name,
                pair => new TrackingResourceSet(pair.Value));
            return new FakeResourceManager(sets);
        }

        public int GetReaderEnumerationCount(string cultureName) =>
            _sets.TryGetValue(cultureName, out var entry) ? entry.EnumerationCount : 0;

        public override ResourceSet GetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents) =>
            _sets.TryGetValue(culture.Name, out var entry) ? entry : null;
    }

    private sealed class TrackingResourceSet : ResourceSet
    {
        private readonly Dictionary<string, string> _entries;

        public int EnumerationCount { get; private set; }

        public TrackingResourceSet(Dictionary<string, string> entries)
        {
            _entries = entries;
        }

        public override IDictionaryEnumerator GetEnumerator()
        {
            EnumerationCount++;
            return ((IDictionary) _entries).GetEnumerator();
        }
    }
}
