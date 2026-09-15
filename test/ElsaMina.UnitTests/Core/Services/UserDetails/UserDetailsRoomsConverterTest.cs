using System.Text.Json;
using ElsaMina.Core.Services.UserDetails;

namespace ElsaMina.UnitTests.Core.Services.UserDetails;

[TestFixture]
public class UserDetailsRoomsConverterTest
{
    [Test]
    public void Test_Deserialize_WhenBooleanFalse_ReturnsNull()
    {
        const string json = "{\"rooms\": false}";
        var result = JsonSerializer.Deserialize<UserDetailsDto>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Rooms, Is.Null);
    }

    [Test]
    public void Test_Deserialize_WhenStringFalse_ReturnsNull()
    {
        const string json = "{\"rooms\": \"false\"}";
        var result = JsonSerializer.Deserialize<UserDetailsDto>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Rooms, Is.Null);
    }

    [Test]
    public void Test_Deserialize_WhenNull_ReturnsNull()
    {
        const string json = "{\"rooms\": null}";
        var result = JsonSerializer.Deserialize<UserDetailsDto>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Rooms, Is.Null);
    }

    [Test]
    public void Test_Deserialize_WhenObject_ReturnsDictionary()
    {
        const string json = """{"rooms": {"lobby": {}, "tournaments": {}}}""";
        var result = JsonSerializer.Deserialize<UserDetailsDto>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Rooms, Is.Not.Null);
        Assert.That(result.Rooms, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Rooms.ContainsKey("lobby"), Is.True);
            Assert.That(result.Rooms.ContainsKey("tournaments"), Is.True);
        }
    }

    [Test]
    public void Test_Serialize_WhenNull_WritesNull()
    {
        var userDetails = new UserDetailsDto { Rooms = null };
        var json = JsonSerializer.Serialize(userDetails);

        Assert.That(json, Does.Contain("\"rooms\":null"));
    }

    [Test]
    public void Test_Serialize_WhenDictionary_WritesObject()
    {
        var userDetails = new UserDetailsDto
        {
            Rooms = new Dictionary<string, UserDetailsRoomDto>
            {
                ["lobby"] = new()
            }
        };
        var json = JsonSerializer.Serialize(userDetails);

        Assert.That(json, Does.Contain("\"rooms\":{\"lobby\":"));
    }
}
