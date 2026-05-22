using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Commands.Misc.Food;

public abstract class RandomFoodPickerCommand : Command
{
    private readonly IRandomService _randomService;

    protected RandomFoodPickerCommand(IRandomService randomService)
    {
        _randomService = randomService;
    }

    public override bool IsAllowedInPrivateMessage => true;
    public override Rank RequiredRank => Rank.Regular;

    protected abstract string[] Items { get; }
    protected abstract int DefaultMinCount { get; }
    protected abstract int DefaultMaxCount { get; }
    protected abstract string InvalidCountMessageKey { get; }
    protected abstract string TooManyItemsMessageKey { get; }

    public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        int count;
        if (!string.IsNullOrWhiteSpace(context.Target))
        {
            if (!int.TryParse(context.Target.Trim(), out count) || count <= 0)
            {
                context.ReplyLocalizedMessage(InvalidCountMessageKey);
                return Task.CompletedTask;
            }

            if (count > Items.Length)
            {
                context.ReplyLocalizedMessage(TooManyItemsMessageKey);
                return Task.CompletedTask;
            }
        }
        else
        {
            count = _randomService.NextInt(DefaultMinCount, DefaultMaxCount);
        }

        var selected = Items.OrderBy(_ => _randomService.NextDouble()).Take(count);
        context.Reply(string.Join(", ", selected));
        return Task.CompletedTask;
    }
}
