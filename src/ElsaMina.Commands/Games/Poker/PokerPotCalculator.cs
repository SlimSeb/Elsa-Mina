namespace ElsaMina.Commands.Games.Poker;

public static class PokerPotCalculator
{
    /// <summary>
    /// Splits the chips committed by every player into a main pot and, when players are all-in for
    /// differing amounts, the appropriate side pots. Folded players still contribute their chips but
    /// are never eligible to win. Pots are returned from the main pot outwards.
    /// </summary>
    public static IReadOnlyList<PokerPot> BuildPots(IReadOnlyList<PokerPlayer> players)
    {
        var contributions = players
            .Where(player => player.Committed > 0)
            .ToDictionary(player => player, player => player.Committed);

        var pots = new List<PokerPot>();

        while (contributions.Count > 0)
        {
            var minContribution = contributions.Values.Min();

            var eligible = contributions.Keys
                .Where(player => !player.HasFolded)
                .Select(player => player.UserId)
                .ToList();

            var amount = minContribution * contributions.Count;

            // Folded-only leftovers (no eligible winner) are merged into the previous pot rather
            // than being lost.
            if (eligible.Count == 0 && pots.Count > 0)
            {
                var previous = pots[^1];
                pots[^1] = new PokerPot(previous.Amount + amount, previous.EligiblePlayerIds);
            }
            else
            {
                pots.Add(new PokerPot(amount, eligible));
            }

            foreach (var player in contributions.Keys.ToList())
            {
                var remaining = contributions[player] - minContribution;
                if (remaining <= 0)
                {
                    contributions.Remove(player);
                }
                else
                {
                    contributions[player] = remaining;
                }
            }
        }

        return pots;
    }
}
