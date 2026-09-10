using System.Text.Json;
using System.Text.Json.Serialization;
using ElsaMina.Core.Services.UserDetails;

namespace ElsaMina.UnitTests.Core.Services.UserDetails;

[TestFixture]
public class UserDetailsRoomsConverterTest
{
    private sealed class TestContainer
    {
        [JsonConverter(typeof(UserDetailsRoomsConverter))]
        public IDictionary<string, UserDetailsRoomDto> Rooms { get; set; }
    }

    [Test]
    public void Test_Deserialize_WhenBooleanFalse_ReturnsNull()
    {
        const string json = "{\"Rooms\": false}";
        var result = JsonSerializer.Deserialize<TestContainer>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Rooms, Is.Null);
    }

    [Test]
    public void Test_Deserialize_WhenStringFalse_ReturnsNull()
    {
        const string json = "{\"Rooms\": \"false\"}";
        var result = JsonSerializer.Deserialize<TestContainer>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Rooms, Is.Null);
    }

    [Test]
    public void Test_Deserialize_WhenNull_ReturnsNull()
    {
        const string json = "{\"Rooms\": null}";
        var result = JsonSerializer.Deserialize<TestContainer>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Rooms, Is.Null);
    }

    [Test]
    public void Test_Deserialize_WhenObject_ReturnsDictionary()
    {
        const string json = """{"Rooms": {"lobby": {}, "tournaments": {}}}""";
        var result = JsonSerializer.Deserialize<TestContainer>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Rooms, Is.Not.Null);
        Assert.That(result.Rooms, Has.Count.EqualTo(2));
        Assert.That(result.Rooms.ContainsKey("lobby"), Is.True);
        Assert.That(result.Rooms.ContainsKey("tournaments"), Is.True);
    }

    [Test]
    public void Test_Serialize_WhenNull_WritesNull()
    {
        var container = new TestContainer { Rooms = null };
        var json = JsonSerializer.Serialize(container);

        Assert.That(json, Does.Contain("\"Rooms\":null"));
    }

    [Test]
    public void Test_Serialize_WhenDictionary_WritesObject()
    {
        var container = new TestContainer
        {
            Rooms = new Dictionary<string, UserDetailsRoomDto>
            {
                ["lobby"] = new()
            }
        };
        var json = JsonSerializer.Serialize(container);

        Assert.That(json, Does.Contain("\"Rooms\":{\"lobby\":"));
    }
}
