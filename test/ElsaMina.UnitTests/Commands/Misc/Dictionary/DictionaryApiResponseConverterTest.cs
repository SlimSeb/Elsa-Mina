using System.Text.Json;
using ElsaMina.Commands.Misc.Dictionary;

namespace ElsaMina.UnitTests.Commands.Misc.Dictionary;

[TestFixture]
public class DictionaryApiResponseConverterTest
{
    [Test]
    public void Test_Deserialize_WhenArrayOfStrings_ReturnsSuggestions()
    {
        const string json = "[\"apple\", \"application\"]";
        var result = JsonSerializer.Deserialize<DictionaryApiResponse>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.HasSuggestions, Is.True);
        Assert.That(result.Suggestions, Is.EqualTo(new List<string> { "apple", "application" }));
        Assert.That(result.Entries, Is.Null);
    }

    [Test]
    public void Test_Deserialize_WhenArrayOfEntries_ReturnsEntries()
    {
        const string json = "[{\"fl\": \"noun\", \"shortdef\": [\"a round fruit\"]}]";
        var result = JsonSerializer.Deserialize<DictionaryApiResponse>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.HasSuggestions, Is.False);
        Assert.That(result.Entries, Has.Count.EqualTo(1));
        Assert.That(result.Entries[0].PartOfSpeech, Is.EqualTo("noun"));
        Assert.That(result.Entries[0].ShortDefinitions, Is.EqualTo(new List<string> { "a round fruit" }));
    }

    [Test]
    public void Test_Deserialize_WhenEmptyArray_ReturnsEmpty()
    {
        const string json = "[]";
        var result = JsonSerializer.Deserialize<DictionaryApiResponse>(json);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.IsEmpty, Is.True);
    }
}
