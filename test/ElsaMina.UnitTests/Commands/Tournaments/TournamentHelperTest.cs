using ElsaMina.Commands.Tournaments;

namespace ElsaMina.UnitTests.Commands.Tournaments;

public class TournamentHelperTest
{
    private const string ROUND_ROBIN_JSON =
        """{"results":[["chinchi !"],["RULT61 Sulfu"],["diptansh V"],["Animeshpokemon21"]],"format":"Troubadour du Fun","generator":"Round Robin","bracketData":{"type":"table","tableHeaders":{"cols":["chinchi !","diptansh V","RULT61 Sulfu","Animeshpokemon21"],"rows":["chinchi !","diptansh V","RULT61 Sulfu","Animeshpokemon21"]},"tableContents":[[null,null,null,null],[{"state":"finished","result":"loss","score":[0,1]},null,null,null],[{"state":"finished","result":"loss","score":[0,0]},{"state":"finished","result":"win","score":[1,0]},null,null],[{"state":"finished","result":"loss","score":[0,1]},{"state":"finished","result":"loss","score":[0,1]},{"state":"finished","result":"loss","score":[3,3]},null]],"scores":[3,1,2,0]}}""";

    [Test]
    public void Test_ParseTourResults_ShouldReturnNull_WhenGeneratorIsUnknown()
    {
        // Arrange
        var json = """{"results":[["PlayerA"]],"format":"Some Format","generator":"Double Elimination","bracketData":{}}""";

        // Act
        var results = TournamentHelper.ParseTourResults(json);

        // Assert
        Assert.That(results, Is.Null);
    }

    [Test]
    public void Test_ParseTourResults_ShouldParseRoundRobinWinner()
    {
        // Act
        var results = TournamentHelper.ParseTourResults(ROUND_ROBIN_JSON);

        // Assert
        Assert.That(results.Winner, Is.EqualTo("chinchi"));
    }

    [Test]
    public void Test_ParseTourResults_ShouldParseRoundRobinRunnerUp()
    {
        // Act
        var results = TournamentHelper.ParseTourResults(ROUND_ROBIN_JSON);

        // Assert
        Assert.That(results.RunnerUp, Is.EqualTo("rult61sulfu"));
    }

    [Test]
    public void Test_ParseTourResults_ShouldParseRoundRobinSemiFinalists()
    {
        // Act
        var results = TournamentHelper.ParseTourResults(ROUND_ROBIN_JSON);

        // Assert
        Assert.That(results.SemiFinalists, Is.EquivalentTo(new List<string> { "diptanshv", "animeshpokemon21" }));
    }

    [Test]
    public void Test_ParseTourResults_ShouldParseRoundRobinFormat()
    {
        // Act
        var results = TournamentHelper.ParseTourResults(ROUND_ROBIN_JSON);

        // Assert
        Assert.That(results.Format, Is.EqualTo("Troubadour du Fun"));
    }

    [Test]
    public void Test_ParseTourResults_ShouldParseRoundRobinPlayers()
    {
        // Act
        var results = TournamentHelper.ParseTourResults(ROUND_ROBIN_JSON);

        // Assert
        Assert.That(results.Players, Is.EquivalentTo(new List<string>
        {
            "chinchi !", "diptansh V", "RULT61 Sulfu", "Animeshpokemon21"
        }));
    }

    [Test]
    public void Test_ParseTourResults_ShouldParseRoundRobinWinsCount()
    {
        // Act
        var results = TournamentHelper.ParseTourResults(ROUND_ROBIN_JSON);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(results.WinsCount["chinchi"], Is.EqualTo(3));
            Assert.That(results.WinsCount["diptanshv"], Is.EqualTo(1));
            Assert.That(results.WinsCount["rult61sulfu"], Is.EqualTo(2));
            Assert.That(results.WinsCount["animeshpokemon21"], Is.Zero);
        }
    }

    [Test]
    public void Test_ParseTourResults_ShouldParseResultsFromJson()
    {
        // Arrange
        var resultJson =
            """{"results":[["Pujolly"]],"format":"Random Inverse Party #2","generator":"Single Elimination","bracketData":{"type":"tree","rootNode":{"children":[{"children":[{"children":[{"children":[{"team":"Emon123"},{"team":"Drafeu-kun"}],"state":"finished","team":"Emon123","result":"win","score":[3,0]},{"team":"palapapop"}],"state":"finished","team":"Emon123","result":"win","score":[1,0]},{"children":[{"team":"Reegychodon_64"},{"team":"Dragonillis"}],"state":"finished","team":"Reegychodon_64","result":"win","score":[1,0]}],"state":"finished","team":"Emon123","result":"win","score":[3,0]},{"children":[{"children":[{"team":"Naiike"},{"team":"Pujolly"}],"state":"finished","team":"Pujolly","result":"loss","score":[5,6]},{"children":[{"team":"le ru c\'est la rue"},{"team":"Bloody jae"}],"state":"finished","team":"Bloody jae","result":"loss","score":[0,2]}],"state":"finished","team":"Pujolly","result":"win","score":[6,1]}],"state":"finished","team":"Pujolly","result":"loss","score":[2,2]}}}""";

        // Act
        var results = TournamentHelper.ParseTourResults(resultJson);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(results.Format, Is.EqualTo("Random Inverse Party #2"));
            Assert.That(results.Winner, Is.EqualTo("pujolly"));
            Assert.That(results.RunnerUp, Is.EqualTo("emon123"));
            Assert.That(results.SemiFinalists, Is.EquivalentTo(new List<string> { "reegychodon64", "bloodyjae" }));
            Assert.That(results.Players, Is.EquivalentTo(new List<string> { "Pujolly", "Emon123", "Drafeu-kun", "palapapop", "Reegychodon_64", "Dragonillis", "Naiike", "Bloody jae", "le ru c'est la rue"}));
            Assert.That(results.WinsCount["pujolly"], Is.EqualTo(3));
            Assert.That(results.WinsCount["emon123"], Is.EqualTo(3));
            Assert.That(results.WinsCount["drafeukun"], Is.Zero);
            Assert.That(results.WinsCount["palapapop"], Is.Zero);
            Assert.That(results.WinsCount["reegychodon64"], Is.EqualTo(1));
            Assert.That(results.WinsCount["dragonillis"], Is.Zero);
            Assert.That(results.WinsCount["naiike"], Is.Zero);
            Assert.That(results.WinsCount["bloodyjae"], Is.EqualTo(1));
            Assert.That(results.WinsCount["lerucestlarue"], Is.Zero);
        }
    }
}