using ElsaMina.Battles.Strategies.MiniPokeV2;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeV2;

/// <summary>
/// history.py's move_outcomes (tools/minipoke/tests/test_move_effects.py reads the same lines).
/// </summary>
public class MoveOutcomesTest
{
    private static readonly string[] LINES =
    [
        "|move|p1a: Garchomp|Earthquake|p2a: Rotom-Wash",
        "|-immune|p2a: Rotom-Wash|[from] ability: Levitate",
        "|turn|2",
        "|move|p1a: Garchomp|Stealth Rock|p2a: Rotom-Wash",
        "|-fail|p1a: Garchomp",
        "|move|p2a: Rotom-Wash|Will-O-Wisp|p1a: Garchomp",
        "|-immune|p2a: Rotom-Wash",
        "|turn|3",
        "|-fail|p1a: Garchomp",
        "|move|p1a: Garchomp|Outrage|p2a: Rotom-Wash|[from]lockedmove",
        "|-fail|p1a: Garchomp"
    ];

    private static MoveOutcomes Read(params string[] lines) =>
        MoveOutcomes.Read(lines.Select(line => line.Split('|')).ToList());

    [Test]
    public void Test_Read_ShouldMarkFailuresAndImmunitiesOfChosenMoves()
    {
        // Act
        var outcomes = Read(LINES);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcomes.FailedLast("p1", "Garchomp", "stealthrock", 1), Is.True);
            Assert.That(outcomes.FailedLast("p1", "Garchomp", "earthquake", 1), Is.False);
            Assert.That(outcomes.FailedLast("p1", "Garchomp", "outrage", 1), Is.False);
            Assert.That(outcomes.FailedLast("p2", "Rotom-Wash", "willowisp", 1), Is.False);
            Assert.That(outcomes.ImmuneBefore("p1", "earthquake", "p2", "Rotom-Wash"), Is.True);
            Assert.That(outcomes.ImmuneBefore("p2", "willowisp", "p1", "Garchomp"), Is.False);
        }
    }

    [Test]
    public void Test_FailedLast_ShouldNeedEveryUseToFail()
    {
        // Arrange: Stealth Rock failed on turn 2, then fails again; then works on turn 4
        string[] twice = [..LINES, "|move|p1a: Garchomp|Stealth Rock|p2a: Rotom-Wash", "|-fail|p1a: Garchomp"];
        string[] worked = [..twice, "|turn|4", "|move|p1a: Garchomp|Stealth Rock|p2a: Rotom-Wash", "|turn|5"];

        // Act
        var failedTwice = Read(twice);
        var workedAfter = Read(worked);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(failedTwice.FailedLast("p1", "Garchomp", "stealthrock", 2), Is.True);
            Assert.That(workedAfter.FailedLast("p1", "Garchomp", "stealthrock", 1), Is.False);
        }
    }

    [Test]
    public void Test_ImmuneBefore_ShouldBeFalse_WhenTargetTerastallizedSince()
    {
        // Act
        var outcomes = Read([..LINES, "|-terastallize|p2a: Rotom-Wash|Fire"]);

        // Assert
        Assert.That(outcomes.ImmuneBefore("p1", "earthquake", "p2", "Rotom-Wash"), Is.False);
    }
}
