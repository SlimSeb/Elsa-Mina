using System.Text.Json;
using System.Text.Json.Serialization;
using ElsaMina.Core.Services.Config;
using ElsaMina.Logging;

namespace ElsaMina.UnitTests.Core.Services.Config;

[TestFixture]
public class ConfigurationJsonTest
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    [Test]
    public void Test_Deserialize_WhenPortIsNumber_AndCommentsArePresent()
    {
        var json = "{\n" +
            "  // Comment here\n" +
            "  \"Host\": \"sim3.psim.us\",\n" +
            "  \"Port\": 443,\n" +
            "  \"LogLevel\": \"Verbose\",\n" +
            "  \"DatabaseRetryDelay\": \"00:00:30\",\n" +
            "  \"Rooms\": [\"botdevelopment\"]\n" +
            "}";

        var config = JsonSerializer.Deserialize<Configuration>(json, _options);

        Assert.That(config, Is.Not.Null);
        Assert.That(config.Host, Is.EqualTo("sim3.psim.us"));
        Assert.That(config.Port, Is.EqualTo("443"));
        Assert.That(config.LogLevel, Is.EqualTo(LogLevel.Verbose));
        Assert.That(config.DatabaseRetryDelay, Is.EqualTo(TimeSpan.FromSeconds(30)));
        Assert.That(config.Rooms, Is.EquivalentTo(new[] { "botdevelopment" }));
    }

    [Test]
    public void Test_Deserialize_WhenPortIsString()
    {
        var json = "{\"Port\": \"8080\"}";

        var config = JsonSerializer.Deserialize<Configuration>(json, _options);

        Assert.That(config, Is.Not.Null);
        Assert.That(config.Port, Is.EqualTo("8080"));
    }
}
