using ElsaMina.Core.Handlers;

namespace ElsaMina.Battles;

public class BattleHandler : Handler
{
    private readonly IBattleService _battleService;

    public BattleHandler(IBattleService battleService)
    {
        _battleService = battleService;
    }

    public override Task HandleReceivedMessageAsync(string[] parts, string roomId = null,
        CancellationToken cancellationToken = default)
    {
        return _battleService.HandleMessageAsync(parts, roomId, cancellationToken);
    }
}
