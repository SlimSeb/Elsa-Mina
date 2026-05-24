using ElsaMina.Commands.Replays;

namespace ElsaMina.UnitTests.Commands.Replays;

public class ReplaysHelperTest
{
    [Test]
    public void Test_GetTeamsFromLog_ShouldReturnEmptyDictionary_WhenLogIsEmpty()
    {
        // Arrange
        var replayLog = string.Empty;

        // Act
        var teams = ReplaysHelper.GetTeamsFromLog(replayLog);

        // Assert
        Assert.That(teams, Is.Empty);
    }

    [Test]
    public void Test_GetTeamsFromLog_ShouldReturnEmptyDictionary_WhenLogContainsNoPokeLines()
    {
        // Arrange
        var replayLog = "This is a test log without any poke lines.";

        // Act
        var teams = ReplaysHelper.GetTeamsFromLog(replayLog);

        // Assert
        Assert.That(teams, Is.Empty);
    }

    [Test]
    public void Test_GetTeamsFromLog_ShouldParseSinglePokeEntry_WhenLogContainsSinglePokeLine()
    {
        // Arrange
        var replayLog = "|poke|p1|Pikachu, M|";

        // Act
        var teams = ReplaysHelper.GetTeamsFromLog(replayLog);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams, Has.Count.EqualTo(1));
            Assert.That(teams.ContainsKey("p1"), Is.True);
            Assert.That(teams["p1"][0], Is.EqualTo("Pikachu"));
        }
    }

    [Test]
    public void Test_GetTeamsFromLog_ShouldParseMultiplePokeEntries_WhenLogContainsMultiplePokeLines()
    {
        // Arrange
        var replayLog = "|poke|p1|Pikachu, M|\n|poke|p1|Charizard, F|\n|poke|p2|Bulbasaur, M|";

        // Act
        var teams = ReplaysHelper.GetTeamsFromLog(replayLog);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams, Has.Count.EqualTo(2));
            Assert.That(teams["p1"], Has.Count.EqualTo(2));
            Assert.That(teams["p2"], Has.Count.EqualTo(1));
            Assert.That(teams["p1"][0], Is.EqualTo("Pikachu"));
            Assert.That(teams["p1"][1], Is.EqualTo("Charizard"));
            Assert.That(teams["p2"][0], Is.EqualTo("Bulbasaur"));
        }
    }

    [Test]
    public void Test_GetTeamsFromLog_ShouldIgnoreNonPokeLines_WhenLogContainsMixedLines()
    {
        // Arrange
        var replayLog = "|poke|p1|Pikachu, M|\n|switch|p1a: Pikachu|Pikachu, L82|\n|poke|p1|Charizard, F|";

        // Act
        var teams = ReplaysHelper.GetTeamsFromLog(replayLog);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams, Has.Count.EqualTo(1));
            Assert.That(teams["p1"], Has.Count.EqualTo(2));
            Assert.That(teams["p1"][0], Is.EqualTo("Pikachu"));
            Assert.That(teams["p1"][1], Is.EqualTo("Charizard"));
        }
    }

    [Test]
    public void Test_GetTeamsFromLog_ShouldParseOnlySpecies_WhenLogContainsAdditionalInfoInPokeLine()
    {
        // Arrange
        var replayLog = "|poke|p1|Pikachu, M|\n|poke|p1|Eevee, Shiny|\n|poke|p2|Charmander, L50|";

        // Act
        var teams = ReplaysHelper.GetTeamsFromLog(replayLog);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams, Has.Count.EqualTo(2));
            Assert.That(teams["p1"][0], Is.EqualTo("Pikachu"));
            Assert.That(teams["p1"][1], Is.EqualTo("Eevee"));
            Assert.That(teams["p2"][0], Is.EqualTo("Charmander"));
        }
    }
}