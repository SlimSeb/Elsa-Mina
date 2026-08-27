using ElsaMina.Battles.Strategies;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Battles.Commands;

[NamedCommand("battlestrategy", "strategy", "decisionstrategy")]
public class BattleStrategyCommand : Command
{
    private readonly IBattleDecisionManager _battleDecisionManager;

    public BattleStrategyCommand(IBattleDecisionManager battleDecisionManager)
    {
        _battleDecisionManager = battleDecisionManager;
    }

    public override bool IsWhitelistOnly => true;
    public override bool IsAllowedInPrivateMessage => true;

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        var target = context.Target?.Trim();

        if (string.IsNullOrWhiteSpace(target))
        {
            var active = _battleDecisionManager.ActiveStrategy;
            var available = string.Join(", ",
                _battleDecisionManager.AvailableStrategies.Select(strategy => strategy.ToString().ToLowerInvariant()));
            context.Reply(
                $"Current battle decision strategy: **{active}**. Available strategies: {available}. Use ``-strategy <name>`` to switch.");
            return Task.CompletedTask;
        }

        if (_battleDecisionManager.TrySetStrategy(target, out var newStrategy))
        {
            context.Reply($"Battle decision strategy switched to: **{newStrategy}**.");
        }
        else
        {
            var available = string.Join(", ",
                _battleDecisionManager.AvailableStrategies.Select(s => s.ToString().ToLowerInvariant()));
            context.Reply($"Unknown strategy \"{target}\". Available strategies: {available}.");
        }

        return Task.CompletedTask;
    }
}