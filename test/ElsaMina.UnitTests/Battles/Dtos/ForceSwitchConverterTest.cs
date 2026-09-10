using System.Text.Json;
using ElsaMina.Battles.Dtos;

namespace ElsaMina.UnitTests.Battles.Dtos;

[TestFixture]
public class ForceSwitchConverterTest
{
    private sealed class TestContainer
    {
        [System.Text.Json.Serialization.JsonConverter(typeof(ForceSwitchConverter))]
        public List<bool> ForceSwitch { get; set; } = [];
    }

    [Test]
    public void Test_Deserialize_WhenArrayOfBooleans()
    {
        const string json = "{\"ForceSwitch\": [true, false, true]}";
        var result = JsonSerializer.Deserialize<TestContainer>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ForceSwitch, Is.EqualTo(new List<bool> { true, false, true }));
    }

    [Test]
    public void Test_Deserialize_WhenBooleanTrue()
    {
        const string json = "{\"ForceSwitch\": true}";
        var result = JsonSerializer.Deserialize<TestContainer>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ForceSwitch, Is.EqualTo(new List<bool> { true }));
    }

    [Test]
    public void Test_Deserialize_WhenBooleanFalse()
    {
        const string json = "{\"ForceSwitch\": false}";
        var result = JsonSerializer.Deserialize<TestContainer>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.ForceSwitch, Is.Empty);
    }

    [Test]
    public void Test_Serialize_WritesArray()
    {
        var container = new TestContainer { ForceSwitch = [true, false] };
        var json = JsonSerializer.Serialize(container);

        Assert.That(json, Does.Contain("[true,false]"));
    }
}
