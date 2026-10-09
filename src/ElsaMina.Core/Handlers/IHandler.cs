namespace ElsaMina.Core.Handlers;

public interface IHandler
{
    bool IsEnabled { get; set; }
    string Identifier { get; }

    /// <summary>
    /// L'ensemble des types de messages que cet handler gère
    /// Retourner un ensemble vide (ou null) pour gérer n'importe quel type de message
    /// </summary>
    IReadOnlySet<string> HandledMessageTypes { get; }

    Task HandleReceivedMessageAsync(string[] parts, string roomId = null, CancellationToken cancellationToken = default);
}