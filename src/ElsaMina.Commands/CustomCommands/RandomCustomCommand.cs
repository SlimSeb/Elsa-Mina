using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.AddedCommands;
using ElsaMina.Core.Services.Commands;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.Logging;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.CustomCommands;

[NamedCommand("randcom", "randcustom", "rand-custom")]
public class RandomCustomCommand : Command
{
    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly IAddedCommandsManager _addedCommandsManager;
    private readonly IRandomService _randomService;

    public RandomCustomCommand(IBotDbContextFactory dbContextFactory, IRandomService randomService,
        IAddedCommandsManager addedCommandsManager)
    {
        _dbContextFactory = dbContextFactory;
        _randomService = randomService;
        _addedCommandsManager = addedCommandsManager;
    }

    public override Rank RequiredRank => Rank.Voiced;
    public override string HelpMessageKey => "randcustom_help";

    public override async Task RunAsync(IContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var commands = await dbContext
                .AddedCommands
                .Where(command => command.RoomId == context.RoomId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var randomCommand = _randomService.RandomElement(commands);
            await _addedCommandsManager.ExecuteAddedCommand(randomCommand, context);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Error while fetching random custom command");
            context.ReplyLocalizedMessage("randcustom_error", exception.Message);
        }
    }
}