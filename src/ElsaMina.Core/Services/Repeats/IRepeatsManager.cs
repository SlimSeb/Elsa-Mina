namespace ElsaMina.Core.Services.Repeats;

public interface IRepeatsManager
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task StartRepeatAsync(string roomId, string message, TimeSpan interval,
        CancellationToken cancellationToken = default);
    IRepeat GetRepeat(Guid repeatId);
    IEnumerable<IRepeat> GetRepeats(string roomId);
    Task<bool> StopRepeatAsync(Guid repeatId, CancellationToken cancellationToken = default);
}
