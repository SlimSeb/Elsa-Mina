using ElsaMina.Commands.Tournaments;

namespace ElsaMina.UnitTests.Commands.Tournaments;

public class TournamentHelperTest
{
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
            Assert.That(results.WinsCount["drafeukun"], Is.EqualTo(0));
            Assert.That(results.WinsCount["palapapop"], Is.EqualTo(0));
            Assert.That(results.WinsCount["reegychodon64"], Is.EqualTo(1));
            Assert.That(results.WinsCount["dragonillis"], Is.EqualTo(0));
            Assert.That(results.WinsCount["naiike"], Is.EqualTo(0));
            Assert.That(results.WinsCount["bloodyjae"], Is.EqualTo(1));
            Assert.That(results.WinsCount["lerucestlarue"], Is.EqualTo(0));
        }
    }
}