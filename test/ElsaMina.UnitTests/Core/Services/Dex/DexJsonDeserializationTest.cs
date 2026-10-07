using System.Text.Json;
using ElsaMina.Core.Services.Dex;

namespace ElsaMina.UnitTests.Core.Services.Dex;

public class DexJsonDeserializationTest
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string POKEMON_JSON = """
        {
          "pokedex_id": 3,
          "name": { "fr": "Florizarre", "en": "Venusaur", "jp": "フシギバナ" },
          "evolution": {
            "pre": [
              { "pokedex_id": 1, "name": "Bulbizarre", "condition": "Niveau 16" },
              { "pokedex_id": 2, "name": "Herbizarre", "condition": "Niveau 32" }
            ],
            "next": null,
            "mega": [
              { "orbe": "Florizarrite", "sprites": { "regular": "mega-regular.png", "shiny": "mega-shiny.png" } }
            ]
          },
          "formes": [
            { "region": "alola", "name": { "fr": "Rattata d'Alola", "en": "Alolan Rattata", "jp": "アローラのコラッタ" } }
          ]
        }
        """;

    [Test]
    public void Test_Deserialize_ShouldMapPreEvolutions_WhenPresent()
    {
        var pokemon = JsonSerializer.Deserialize<Pokemon>(POKEMON_JSON, JSON_OPTIONS);

        Assert.That(pokemon.Evolution.PreEvolution, Has.Count.EqualTo(2));
        Assert.That(pokemon.Evolution.PreEvolution[1].PokedexId, Is.EqualTo(2));
        Assert.That(pokemon.Evolution.PreEvolution[1].Name, Is.EqualTo("Herbizarre"));
        Assert.That(pokemon.Evolution.PreEvolution[1].Condition, Is.EqualTo("Niveau 32"));
        Assert.That(pokemon.Evolution.NextEvolutions, Is.Null);
    }

    [Test]
    public void Test_Deserialize_ShouldMapMegaEvolutions_WhenPresent()
    {
        var pokemon = JsonSerializer.Deserialize<Pokemon>(POKEMON_JSON, JSON_OPTIONS);

        Assert.That(pokemon.Evolution.MegaEvolution, Has.Count.EqualTo(1));
        Assert.That(pokemon.Evolution.MegaEvolution[0].Orb, Is.EqualTo("Florizarrite"));
        Assert.That(pokemon.Evolution.MegaEvolution[0].Sprites.Shiny, Is.EqualTo("mega-shiny.png"));
    }

    [Test]
    public void Test_Deserialize_ShouldMapFormes_WhenPresent()
    {
        var pokemon = JsonSerializer.Deserialize<Pokemon>(POKEMON_JSON, JSON_OPTIONS);

        Assert.That(pokemon.Formes, Has.Count.EqualTo(1));
        Assert.That(pokemon.Formes[0].Region, Is.EqualTo("alola"));
        Assert.That(pokemon.Formes[0].Name.English, Is.EqualTo("Alolan Rattata"));
    }

    [Test]
    public void Test_Deserialize_ShouldLeaveOptionalCollectionsNull_WhenJsonValuesAreNull()
    {
        const string json = """{ "pokedex_id": 1, "evolution": { "pre": null, "next": null, "mega": null }, "formes": null }""";

        var pokemon = JsonSerializer.Deserialize<Pokemon>(json, JSON_OPTIONS);

        Assert.That(pokemon.Evolution.PreEvolution, Is.Null);
        Assert.That(pokemon.Evolution.MegaEvolution, Is.Null);
        Assert.That(pokemon.Formes, Is.Null);
    }

    [Test]
    public void Test_Deserialize_ShouldReadNumericAccuracy_WhenMoveCanMiss()
    {
        var move = JsonSerializer.Deserialize<MoveData>("""{ "name": "Tackle", "accuracy": 100 }""", JSON_OPTIONS);

        Assert.That(move.Accuracy, Is.EqualTo(100));
    }

    [Test]
    public void Test_Deserialize_ShouldReadNullAccuracy_WhenMoveNeverMisses()
    {
        var move = JsonSerializer.Deserialize<MoveData>("""{ "name": "Swift", "accuracy": true }""", JSON_OPTIONS);

        Assert.That(move.Accuracy, Is.Null);
    }

    [Test]
    public void Test_Deserialize_ShouldReadNullAccuracy_WhenAccuracyIsMissing()
    {
        var move = JsonSerializer.Deserialize<MoveData>("""{ "name": "Swift" }""", JSON_OPTIONS);

        Assert.That(move.Accuracy, Is.Null);
    }

    [Test]
    public void Test_Serialize_ShouldWriteTrue_WhenAccuracyIsNull()
    {
        var json = JsonSerializer.Serialize(new MoveData { Accuracy = null }, JSON_OPTIONS);

        Assert.That(json, Does.Contain("\"accuracy\":true"));
    }
}
