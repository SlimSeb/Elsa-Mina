namespace ElsaMina.Core;

public interface IBot : IDisposable
{
    /// <summary>
    /// Traite une frame reçue du serveur. Si on reçoit des frames en parallèle faut passer par
    /// <see cref="Services.Dispatch.IIncomingMessageDispatcher"/>, qui garde les frames de chaque room dans l'ordre
    /// </summary>
    Task HandleReceivedMessageAsync(string message);
    void Send(string message);
    void Say(string roomId, string message);
    Task StartAsync();
    void OnReconnect();
    void OnDisconnect();

    /// <summary>
    /// Lance le taf d'arrêt (sauvegardes en attente, messages en file) et l'attend, jusqu'à ce que <paramref name="cancellationToken"/> se déclenche
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
    TimeSpan UpTime { get; }
}
