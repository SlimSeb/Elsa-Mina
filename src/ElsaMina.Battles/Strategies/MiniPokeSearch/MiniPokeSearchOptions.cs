namespace ElsaMina.Battles.Strategies.MiniPokeSearch;

/// <summary>
/// How much the minipoke search looks at (tools/minipoke/search.py's SearchConfig, the settings it was measured with).
/// </summary>
public sealed record MiniPokeSearchOptions
{
    /// <summary>Our candidate actions, the model's most likely first.</summary>
    public int TopK { get; init; } = 3;

    /// <summary>
    /// ...that the model's policy gives at least this probability (the first one always). 0: a model sure of a bad
    /// move still gets alternatives, which measured better (61.9% vs 57.2% against the model alone).
    /// </summary>
    public double MinProbability { get; init; }

    /// <summary>Guesses of the opponent's sets.</summary>
    public int Determinizations { get; init; } = 2;

    /// <summary>Simulated turns per pair of actions and guess.</summary>
    public int Samples { get; init; } = 1;

    public double Gamma { get; init; } = 0.995;

    /// <summary>The faint reward the models were trained with, added to each simulated turn's value.</summary>
    public double FaintReward { get; init; } = 0.1;

    /// <summary>Weight of the log policy probability added to each candidate's score: it breaks near ties.</summary>
    public double PriorWeight { get; init; } = 0.02;

    public int Iterations { get; init; } = 400;

    /// <summary>Past this, the decision is the model's own.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(8);
}
