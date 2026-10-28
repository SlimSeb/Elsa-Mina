namespace ElsaMina.Battles.Strategies.MiniPokeSearch;

/// <summary>
/// The equilibrium of a small zero-sum game by regret matching+ (tools/minipoke/search.py's solve_zero_sum): the
/// weighted averages of the iterates converge to the row player's maximin mix and the column player's minimax mix.
/// </summary>
public static class ZeroSumSolver
{
    /// <summary>
    /// The mixes (ours, theirs) of a game where we maximize <paramref name="payoff"/>[our action, their action].
    /// </summary>
#pragma warning disable S2368 // a payoff matrix is the natural argument of a matrix game solver
    public static (double[] Ours, double[] Theirs) Solve(double[,] payoff, int iterations)
#pragma warning restore S2368
    {
        var rows = payoff.GetLength(0);
        var columns = payoff.GetLength(1);
        var rowRegrets = new double[rows];
        var columnRegrets = new double[columns];
        var rowSums = new double[rows];
        var columnSums = new double[columns];
        for (var iteration = 1; iteration <= iterations; iteration++)
        {
            var ours = Normalize(rowRegrets);
            var theirs = Normalize(columnRegrets);
            for (var i = 0; i < rows; i++)
            {
                rowSums[i] += iteration * ours[i];
            }

            for (var j = 0; j < columns; j++)
            {
                columnSums[j] += iteration * theirs[j];
            }

            var rowValues = new double[rows];
            var columnValues = new double[columns];
            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < columns; j++)
                {
                    rowValues[i] += payoff[i, j] * theirs[j];
                    columnValues[j] -= payoff[i, j] * ours[i];
                }
            }

            var rowValue = Dot(ours, rowValues);
            var columnValue = Dot(theirs, columnValues);
            for (var i = 0; i < rows; i++)
            {
                rowRegrets[i] = Math.Max(rowRegrets[i] + rowValues[i] - rowValue, 0);
            }

            for (var j = 0; j < columns; j++)
            {
                columnRegrets[j] = Math.Max(columnRegrets[j] + columnValues[j] - columnValue, 0);
            }
        }

        return (Normalize(rowSums), Normalize(columnSums));
    }

    private static double[] Normalize(double[] weights)
    {
        var total = weights.Sum();
        return total > 0
            ? weights.Select(weight => weight / total).ToArray()
            : Enumerable.Repeat(1.0 / weights.Length, weights.Length).ToArray();
    }

    private static double Dot(double[] left, double[] right) => left.Select((value, i) => value * right[i]).Sum();
}
