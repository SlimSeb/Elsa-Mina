namespace ElsaMina.Battles;

public interface IBattleService
{
    Task HandleMessageAsync(string[] parts, string roomId, CancellationToken cancellationToken = default);
}
