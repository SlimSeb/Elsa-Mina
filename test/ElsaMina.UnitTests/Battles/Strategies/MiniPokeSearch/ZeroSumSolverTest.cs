using ElsaMina.Battles.Strategies.MiniPokeSearch;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeSearch;

public class ZeroSumSolverTest
{
    private const int ITERATIONS = 2000;

    [Test]
    public void Test_Solve_ShouldMixEvenly_WhenGameIsMatchingPennies()
    {
        // Arrange
        var pennies = new double[,] { { 1, -1 }, { -1, 1 } };

        // Act
        var (ours, theirs) = ZeroSumSolver.Solve(pennies, ITERATIONS);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ours[0], Is.EqualTo(0.5).Within(0.03));
            Assert.That(theirs[0], Is.EqualTo(0.5).Within(0.03));
        }
    }

    [Test]
    public void Test_Solve_ShouldPlayPureStrategies_WhenOneActionDominates()
    {
        // Arrange: our first row is better whatever they do, their first column hurts us least
        var dominated = new double[,] { { 1, 2 }, { 0, 0.5 } };

        // Act
        var (ours, theirs) = ZeroSumSolver.Solve(dominated, ITERATIONS);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ours[0], Is.GreaterThan(0.97));
            Assert.That(theirs[0], Is.GreaterThan(0.97));
        }
    }
}
