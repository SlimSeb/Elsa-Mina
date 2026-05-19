using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.Legacy;

[NamedCommand("election", Aliases = ["elections"])]
public class ElectionCommand : Command
{
    private readonly IRandomService _randomService;

    public ElectionCommand(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public override Rank RequiredRank => Rank.Regular;
    public override bool IsAllowedInPrivateMessage => true;
    public override string HelpMessageKey => "election_help";

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var candidates = context.Target
            .Split(',')
            .Select(candidate => candidate.Trim())
            .Where(candidate => !string.IsNullOrEmpty(candidate))
            .ToArray();

        if (candidates.Length == 0)
        {
            return Task.CompletedTask;
        }

        var scores = candidates.ToDictionary(candidate => candidate, _ => _randomService.NextDouble());
        var total = scores.Values.Sum();
        var factor = 100.0 / total;

        var results = scores
            .OrderByDescending(pair => pair.Value)
            .Select(pair => $"{pair.Key}: **{pair.Value * factor:F2}%**");

        context.Reply("Results: " + string.Join(' ', results), rankAware: true);
        return Task.CompletedTask;
    }
}