using ElsaMina.Battles.Strategies.MiniPokeSearch;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeSearch;

/// <summary>
/// The position search_sim.js rebuilds (tools/minipoke/tests/test_search.py checks Python's position_of the same way).
/// </summary>
public class SearchPositionBuilderTest
{
    private SearchTestBattle _battle;

    [SetUp]
    public void SetUp()
    {
        _battle = new SearchTestBattle();
    }

    [Test]
    public void Test_Build_ShouldDescribeOurTeamFromTheRequest()
    {
        // Act
        var position = SearchPositionBuilder.Build(_battle.Context);

        // Assert
        var ours = position["sides"]["p1"];
        var sandaconda = ours["pokemon"][0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That((string)position["us"], Is.EqualTo("p1"));
            Assert.That((int)position["turn"], Is.EqualTo(1));
            Assert.That((string)sandaconda["name"], Is.EqualTo("Sandaconda"));
            Assert.That((int)sandaconda["level"], Is.EqualTo(84));
            Assert.That((int)sandaconda["hp"], Is.EqualTo(200));
            Assert.That((int)sandaconda["maxhp"], Is.EqualTo(258));
            Assert.That((int)sandaconda["stats"]["spe"], Is.EqualTo(170));
            Assert.That((int)sandaconda["moves"][0]["pp"], Is.EqualTo(12));
            Assert.That((string)sandaconda["teraType"], Is.EqualTo("Water"));
            Assert.That((bool)sandaconda["active"], Is.True);
            Assert.That(ours["pokemon"][1]["moves"][0]["pp"], Is.Null);
            Assert.That((int)ours["size"], Is.EqualTo(2));
        }
    }

    [Test]
    public void Test_Build_ShouldDescribeWhatTheOpponentRevealed()
    {
        // Arrange
        _battle.Apply("|move|p2a: Eelektross|Coil|p2a: Eelektross");
        _battle.Apply("|-boost|p2a: Eelektross|atk|1");
        _battle.Apply("|-sidestart|p1: Elsa|move: Stealth Rock");
        _battle.Apply("|-damage|p2a: Eelektross|70/100");

        // Act
        var position = SearchPositionBuilder.Build(_battle.Context);

        // Assert
        var theirs = position["sides"]["p2"];
        var eelektross = theirs["pokemon"][0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That((string)eelektross["name"], Is.EqualTo("Eelektross"));
            Assert.That((string)eelektross["moves"][0]["id"], Is.EqualTo("coil"));
            Assert.That((double)eelektross["hpFraction"], Is.EqualTo(0.7).Within(1e-9));
            Assert.That((int)eelektross["boosts"]["atk"], Is.EqualTo(1));
            Assert.That(eelektross["item"], Is.Null);
            Assert.That((int)theirs["size"], Is.EqualTo(6));
            Assert.That((int)position["sides"]["p1"]["conditions"]["stealthrock"], Is.EqualTo(1));
        }
    }

    [Test]
    public void Test_NameOf_ShouldKeepTheNameAfterTheSide()
    {
        // Act & Assert
        Assert.That(SearchPositionBuilder.NameOf("p2a: Iron Valiant"), Is.EqualTo("Iron Valiant"));
    }
}
