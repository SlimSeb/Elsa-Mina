using ElsaMina.Commands.Teams;
using ElsaMina.Core.Utils;
using Newtonsoft.Json;

namespace ElsaMina.UnitTests.Core.Utils;

public class ShowdownTeamsUtilsTest
{
    // TODO : make the bot cross-platform as this doesn't work on Windows
    
    [Test]
    public void Test_DeserializeTeamExport_ShouldReturnEmptyTeam_WhenExportIsEmpty()
    {
        // Arrange
        var export = string.Empty;

        // Act
        var result = ShowdownTeamsUtils.DeserializeTeamExport(export);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Test_DeserializeTeamExport_ShouldParseSinglePokemon_WhenValidSinglePokemonExportIsGiven()
    {
        // Arrange
        const string export = """
                              Pikachu @ Light Ball
                              Ability: Static
                              EVs: 252 Atk / 4 SpD / 252 Spe
                              Jolly Nature
                              - Volt Tackle
                              - Iron Tail
                              """;

        // Act
        var result = ShowdownTeamsUtils.DeserializeTeamExport(export).ToList();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        var pokemonSet = result.First();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pokemonSet.Species, Is.EqualTo("Pikachu"));
            Assert.That(pokemonSet.Item, Is.EqualTo("Light Ball"));
            Assert.That(pokemonSet.Ability, Is.EqualTo("Static"));
            Assert.That(pokemonSet.Nature, Is.EqualTo("Jolly"));
            Assert.That(pokemonSet.EffortValues["atk"], Is.EqualTo(252));
            Assert.That(pokemonSet.EffortValues["spe"], Is.EqualTo(252));
            Assert.That(pokemonSet.Moves, Is.EquivalentTo(new List<string> { "Volt Tackle", "Iron Tail" }));
        }
    }

    [Test]
    public void Test_GetSetExport_ShouldGenerateCorrectFormat_WhenPokemonSetIsGiven()
    {
        // Arrange
        var pokemonSet = new PokemonSet
        {
            Species = "Pikachu",
            Item = "Light Ball",
            Ability = "Static",
            Nature = "Jolly",
            EffortValues = new Dictionary<string, int> { { "atk", 252 }, { "spe", 252 } },
            Moves = new List<string> { "Volt Tackle", "Iron Tail" }
        };

        // Act
        var result = ShowdownTeamsUtils.GetSetExport(pokemonSet);

        // Assert
        const string expectedExport = """
                                      Pikachu @ Light Ball
                                      Ability: Static 
                                      EVs: 252 Atk / 252 Spe
                                      Jolly Nature 
                                      - Volt Tackle
                                      - Iron Tail
                                      """;
        Assert.That(result.Trim(), Is.EqualTo(expectedExport));
    }

    [Test]
    public void Test_TeamExportToJson_ShouldReturnJsonRepresentation_WhenExportIsGiven()
    {
        // Arrange
        const string export = """
                              Pikachu @ Light Ball
                              Ability: Static
                              EVs: 252 Atk / 4 SpD / 252 Spe
                              Jolly Nature
                              - Volt Tackle
                              - Iron Tail
                              """;

        var expectedJson = JsonConvert.SerializeObject(ShowdownTeamsUtils.DeserializeTeamExport(export));

        // Act
        var result = ShowdownTeamsUtils.TeamExportToJson(export);

        // Assert
        Assert.That(result, Is.EqualTo(expectedJson));
    }


    [Test]
    public void Test_DeserializeTeamExport_ShouldParseMultiplePokemon_WhenMultiplePokemonExportIsGiven()
    {
        // Arrange
        const string export = """
                              Pikachu @ Light Ball
                              Ability: Static
                              EVs: 252 Atk / 4 SpD / 252 Spe
                              Jolly Nature
                              - Volt Tackle
                              - Iron Tail

                              Charizard @ Charizardite X
                              Ability: Blaze
                              EVs: 4 HP / 252 Atk / 252 Spe
                              Adamant Nature
                              - Flare Blitz
                              - Dragon Claw
                              """;

        // Act
        var result = ShowdownTeamsUtils.DeserializeTeamExport(export).ToList();

        // Assert
        var expectedMoves = new[] { "Flare Blitz", "Dragon Claw" };
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Species, Is.EqualTo("Pikachu"));
            Assert.That(result[1].Species, Is.EqualTo("Charizard"));
            Assert.That(result[1].Item, Is.EqualTo("Charizardite X"));
            Assert.That(result[1].Ability, Is.EqualTo("Blaze"));
            Assert.That(result[1].Nature, Is.EqualTo("Adamant"));
            Assert.That(result[1].Moves, Is.EquivalentTo(expectedMoves));
        }
    }

    [Test]
    public void Test_DeserializeTeamExport_ShouldParseAllOptionalFields_WhenFullExportIsGiven()
    {
        // Arrange
        const string export = """
                              Nurse (Blissey) (F) @ Leftovers
                              Ability: Natural Cure
                              Level: 50
                              Shiny: Yes
                              Happiness: 100
                              Pokeball: Poke Ball
                              Tera Type: Water
                              EVs: 252 HP / 4 Def / 252 SpD
                              IVs: 0 Atk / 30 Spe
                              Calm Nature
                              - Soft-Boiled
                              - Seismic Toss
                              """;

        // Act
        var pokemonSet = ShowdownTeamsUtils.DeserializeTeamExport(export).Single();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pokemonSet.Name, Is.EqualTo("Nurse"));
            Assert.That(pokemonSet.Species, Is.EqualTo("Blissey"));
            Assert.That(pokemonSet.Gender, Is.EqualTo("F"));
            Assert.That(pokemonSet.Item, Is.EqualTo("Leftovers"));
            Assert.That(pokemonSet.Ability, Is.EqualTo("Natural Cure"));
            Assert.That(pokemonSet.Level, Is.EqualTo(50));
            Assert.That(pokemonSet.IsShiny, Is.True);
            Assert.That(pokemonSet.Happiness, Is.EqualTo(100));
            Assert.That(pokemonSet.Pokeball, Is.EqualTo("Poke Ball"));
            Assert.That(pokemonSet.TeraType, Is.EqualTo("Water"));
            Assert.That(pokemonSet.Nature, Is.EqualTo("Calm"));
            Assert.That(pokemonSet.EffortValues["hp"], Is.EqualTo(252));
            Assert.That(pokemonSet.EffortValues["def"], Is.EqualTo(4));
            Assert.That(pokemonSet.EffortValues["spd"], Is.EqualTo(252));
            Assert.That(pokemonSet.EffortValues["atk"], Is.EqualTo(0));
            Assert.That(pokemonSet.IndividualValues["atk"], Is.EqualTo(0));
            Assert.That(pokemonSet.IndividualValues["spe"], Is.EqualTo(30));
            Assert.That(pokemonSet.IndividualValues["hp"], Is.EqualTo(31));
            Assert.That(pokemonSet.Moves, Is.EquivalentTo(new[] { "Soft-Boiled", "Seismic Toss" }));
        }
    }

    [Test]
    public void Test_DeserializeTeamExport_ShouldParseMaleGender_WhenGenderMarkerIsPresent()
    {
        // Arrange
        const string export = """
                              Gyarados (M) @ Choice Band
                              Ability: Intimidate
                              - Waterfall
                              """;

        // Act
        var pokemonSet = ShowdownTeamsUtils.DeserializeTeamExport(export).Single();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pokemonSet.Species, Is.EqualTo("Gyarados"));
            Assert.That(pokemonSet.Gender, Is.EqualTo("M"));
        }
    }

    [Test]
    public void Test_DeserializeTeamExport_ShouldClearItem_WhenItemIsNoItem()
    {
        // Arrange
        const string export = """
                              Ditto @ No Item
                              Ability: Imposter
                              - Transform
                              """;

        // Act
        var pokemonSet = ShowdownTeamsUtils.DeserializeTeamExport(export).Single();

        // Assert
        Assert.That(pokemonSet.Item, Is.Empty);
    }

    [Test]
    public void Test_GetSetExport_ShouldIncludeAllOptionalFields_WhenSetIsFullyPopulated()
    {
        // Arrange
        var pokemonSet = new PokemonSet
        {
            Name = "Nurse",
            Species = "Blissey",
            Gender = "F",
            Item = "Leftovers",
            Ability = "Natural Cure",
            Level = 50,
            IsShiny = true,
            Happiness = 100,
            Nature = "Calm",
            EffortValues = new Dictionary<string, int> { { "hp", 252 }, { "def", 4 }, { "spd", 252 } },
            IndividualValues = new Dictionary<string, int>
            {
                { "hp", 31 }, { "atk", 0 }, { "def", 31 }, { "spa", 31 }, { "spd", 31 }, { "spe", 30 }
            },
            Moves = new List<string> { "Soft-Boiled", "Seismic Toss" }
        };

        // Act
        var result = ShowdownTeamsUtils.GetSetExport(pokemonSet);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Does.StartWith("Nurse (Blissey) (F) @ Leftovers"));
            Assert.That(result, Does.Contain("Ability: Natural Cure"));
            Assert.That(result, Does.Contain("Level: 50"));
            Assert.That(result, Does.Contain("Shiny: Yes"));
            Assert.That(result, Does.Contain("Happiness: 100"));
            Assert.That(result, Does.Contain("EVs: 252 HP / 4 Def / 252 SpD"));
            Assert.That(result, Does.Contain("Calm Nature"));
            Assert.That(result, Does.Contain("IVs: 0 Atk / 30 Spe"));
            Assert.That(result, Does.Contain("- Soft-Boiled"));
            Assert.That(result, Does.Contain("- Seismic Toss"));
        }
    }

    [Test]
    public void Test_UnpackTeam_ShouldReturnEmptyTeam_WhenBufferIsNullOrEmpty()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ShowdownTeamsUtils.UnpackTeam(null), Is.Empty);
            Assert.That(ShowdownTeamsUtils.UnpackTeam(string.Empty), Is.Empty);
        }
    }

    [Test]
    public void Test_PackTeam_ShouldReturnEmptyString_WhenTeamIsNullOrEmpty()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ShowdownTeamsUtils.PackTeam(null), Is.Empty);
            Assert.That(ShowdownTeamsUtils.PackTeam(new List<PokemonSet>()), Is.Empty);
        }
    }

    [Test]
    public void Test_UnpackTeam_ShouldParsePackedSet_WhenValidPackedStringIsGiven()
    {
        // Arrange
        // name|species|item|ability|moves|nature|evs|gender|ivs|shiny|level|misc
        const string packed = "Pika|pikachu|lightball|static|volttackle,irontail|Jolly|0,252,0,0,4,252||||";

        // Act
        var pokemonSet = ShowdownTeamsUtils.UnpackTeam(packed).Single();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pokemonSet.Name, Is.EqualTo("Pika"));
            Assert.That(pokemonSet.Species, Is.EqualTo("pikachu"));
            Assert.That(pokemonSet.Item, Is.EqualTo("lightball"));
            Assert.That(pokemonSet.Ability, Is.EqualTo("static"));
            Assert.That(pokemonSet.Nature, Is.EqualTo("Jolly"));
            Assert.That(pokemonSet.Moves, Is.EquivalentTo(new[] { "volttackle", "irontail" }));
            Assert.That(pokemonSet.EffortValues["atk"], Is.EqualTo(252));
            Assert.That(pokemonSet.EffortValues["spd"], Is.EqualTo(4));
            Assert.That(pokemonSet.EffortValues["spe"], Is.EqualTo(252));
        }
    }

    [Test]
    public void Test_PackTeam_ShouldBeIdempotent_WhenRoundTrippedThroughUnpack()
    {
        // Arrange
        var team = new List<PokemonSet>
        {
            new()
            {
                Name = "Pika",
                Species = "Pikachu",
                Item = "Light Ball",
                Ability = "Static",
                Nature = "Jolly",
                Level = 50,
                IsShiny = true,
                EffortValues = new Dictionary<string, int>
                {
                    { "hp", 0 }, { "atk", 252 }, { "def", 0 }, { "spa", 0 }, { "spd", 4 }, { "spe", 252 }
                },
                IndividualValues = new Dictionary<string, int>
                {
                    { "hp", 31 }, { "atk", 0 }, { "def", 31 }, { "spa", 31 }, { "spd", 31 }, { "spe", 31 }
                },
                Moves = new List<string> { "Volt Tackle", "Iron Tail" }
            },
            new()
            {
                Species = "Charizard",
                Item = "Charizardite X",
                Ability = "Blaze",
                Nature = "Adamant",
                Moves = new List<string> { "Flare Blitz", "Dragon Claw" }
            }
        };

        // Act
        var packedOnce = ShowdownTeamsUtils.PackTeam(team);
        var packedTwice = ShowdownTeamsUtils.PackTeam(ShowdownTeamsUtils.UnpackTeam(packedOnce));

        // Assert
        Assert.That(packedTwice, Is.EqualTo(packedOnce));
    }

    [Test]
    public void Test_PackThenUnpack_ShouldPreserveKeyFields_WhenTeamHasSingleSet()
    {
        // Arrange
        var original = new PokemonSet
        {
            Species = "Great Tusk",
            Item = "Booster Energy",
            Ability = "Protosynthesis",
            Nature = "Jolly",
            Moves = new List<string> { "Headlong Rush", "Close Combat" }
        };

        // Act
        var packed = ShowdownTeamsUtils.PackTeam(new List<PokemonSet> { original });
        var unpacked = ShowdownTeamsUtils.UnpackTeam(packed).Single();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            // With no nickname the name field carries the species verbatim, so casing is preserved.
            Assert.That(unpacked.Species, Is.EqualTo("Great Tusk"));
            Assert.That(unpacked.Item, Is.EqualTo("boosterenergy"));
            Assert.That(unpacked.Ability, Is.EqualTo("protosynthesis"));
            Assert.That(unpacked.Nature, Is.EqualTo("Jolly"));
            Assert.That(unpacked.Moves, Is.EquivalentTo(new[] { "headlongrush", "closecombat" }));
        }
    }

    [Test]
    public void Test_GetTeamExport_ShouldJoinSetsWithBlankLine_WhenMultipleSetsAreGiven()
    {
        // Arrange
        var sets = new List<PokemonSet>
        {
            new() { Species = "Pikachu", Moves = new List<string> { "Volt Tackle" } },
            new() { Species = "Charizard", Moves = new List<string> { "Flare Blitz" } }
        };

        // Act
        var result = ShowdownTeamsUtils.GetTeamExport(sets);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Does.Contain("Pikachu"));
            Assert.That(result, Does.Contain("Charizard"));
            Assert.That(result, Does.Contain("\n\n"));
        }
    }
}