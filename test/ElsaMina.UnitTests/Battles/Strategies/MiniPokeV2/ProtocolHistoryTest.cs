using ElsaMina.Battles.Strategies.MiniPokeV2;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeV2;

/// <summary>
/// history.py's reading of the protocol log. We are p1, the opponent p2.
/// </summary>
public class ProtocolHistoryTest
{
    private static readonly string[] SECOND_TURN = ["|move|p2a: Iron Valiant|Moonblast|p1a: Garchomp"];
    private static readonly string[] SAME_MOVE_TWICE = ["Outrage", "Outrage"];

    private static List<string[]> Lines(params string[] lines) => lines.Select(line => line.Split('|')).ToList();

    [Test]
    public void Test_PreviousTurn_ShouldReturnLinesBetweenLastTwoTurns_WhenTurnIsOngoing()
    {
        // Arrange: a replacement mid-turn still looks at the last complete turn
        var lines = Lines("|turn|1", "|move|p1a: Garchomp|Earthquake|p2a: Iron Valiant", "|turn|2",
            "|move|p2a: Iron Valiant|Moonblast|p1a: Garchomp", "|turn|3", "|faint|p1a: Garchomp");

        // Act
        var previousTurn = ProtocolHistory.PreviousTurn(lines);

        // Assert
        Assert.That(previousTurn.Select(line => string.Join('|', line)), Is.EqualTo(SECOND_TURN));
    }

    [Test]
    public void Test_PreviousTurn_ShouldBeEmpty_WhenBeforeTurnTwo()
    {
        // Act
        var previousTurn = ProtocolHistory.PreviousTurn(Lines("|switch|p1a: Garchomp|Garchomp, L80|100/100", "|turn|1"));

        // Assert
        Assert.That(previousTurn, Is.Empty);
    }

    [Test]
    public void Test_SwitchedByChoice_ShouldBeTrue_WhenSideSwitchedBeforeMoving()
    {
        // Arrange
        var turn = Lines("|switch|p2a: Corviknight|Corviknight, L80|100/100",
            "|move|p1a: Garchomp|Earthquake|p2a: Corviknight");

        // Act & Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ProtocolHistory.SwitchedByChoice(turn, "p2"), Is.True);
            Assert.That(ProtocolHistory.SwitchedByChoice(turn, "p1"), Is.False);
        }
    }

    [TestCase("|move|p2a: Iron Valiant|U-turn|p1a: Garchomp")]
    [TestCase("|faint|p2a: Iron Valiant")]
    public void Test_SwitchedByChoice_ShouldBeFalse_WhenSwitchCameAfterMoveOrFaint(string firstLine)
    {
        // Arrange
        var turn = Lines(firstLine, "|switch|p2a: Corviknight|Corviknight, L80|100/100");

        // Act
        var switched = ProtocolHistory.SwitchedByChoice(turn, "p2");

        // Assert
        Assert.That(switched, Is.False);
    }

    [Test]
    public void Test_SameMoveTwice_ShouldSkipCalledMovesAndStruggle_WhenLookingForChoices()
    {
        // Arrange
        var lines = Lines("|switch|p2a: Dragonite|Dragonite, L74|100/100", "|move|p2a: Dragonite|Outrage|p1a: Garchomp",
            "|turn|2", "|move|p2a: Dragonite|Outrage|p1a: Garchomp|[from]lockedmove", "|turn|3",
            "|move|p2a: Dragonite|Struggle|p1a: Garchomp", "|turn|4", "|move|p2a: Dragonite|Outrage|p1a: Garchomp");

        // Act
        var moves = ProtocolHistory.LastChosenMoves(lines, "p2", 2);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(moves, Is.EqualTo(SAME_MOVE_TWICE));
            Assert.That(ProtocolHistory.SameMoveTwice(lines, "p2"), Is.True);
        }
    }

    [TestCase("switch")]
    [TestCase("drag")]
    [TestCase("replace")]
    public void Test_SameMoveTwice_ShouldOnlyLookSinceLastEntrance_WhenPokemonCameIn(string entrance)
    {
        // Arrange
        var lines = Lines("|move|p2a: Dragonite|Outrage|p1a: Garchomp", "|turn|2",
            $"|{entrance}|p2a: Dragonite|Dragonite, L74|100/100", "|move|p2a: Dragonite|Outrage|p1a: Garchomp");

        // Act
        var sameMoveTwice = ProtocolHistory.SameMoveTwice(lines, "p2");

        // Assert
        Assert.That(sameMoveTwice, Is.False);
    }

    [TestCase("p1", "p2")]
    [TestCase("p2", "p1")]
    [TestCase(null, null)]
    public void Test_OpponentRole_ShouldReturnOtherSide_WhenRoleIsKnown(string role, string expected)
    {
        // Act
        var opponentRole = ProtocolHistory.OpponentRole(role);

        // Assert
        Assert.That(opponentRole, Is.EqualTo(expected));
    }
}
