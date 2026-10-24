using ElsaMina.Battles.Strategies;

namespace ElsaMina.Battles.Results;

/// <summary>
/// The bot's wins, losses and ties in a format with a strategy since it started.
/// </summary>
public sealed record BattleRecord(string Format, BattleDecisionStrategy Strategy, int Wins, int Losses, int Ties)
{
    public int Played => Wins + Losses + Ties;

    public BattleRecord Add(BattleOutcome outcome) => outcome switch
    {
        BattleOutcome.Won => this with { Wins = Wins + 1 },
        BattleOutcome.Lost => this with { Losses = Losses + 1 },
        _ => this with { Ties = Ties + 1 }
    };
}
