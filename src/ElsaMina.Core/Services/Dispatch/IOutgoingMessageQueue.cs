namespace ElsaMina.Core.Services.Dispatch;

public interface IOutgoingMessageQueue
{
    /// <summary>
    /// Envoie <paramref name="message"/> direct si le cooldown d'envoi le permet, sinon le met en file.
    /// Les messages en file partent dans l'ordre, espacés d'un cooldown, peu importe le thread qui les a mis.
    /// Le même message renvoyé pendant la fenêtre anti-doublon passe à la trappe.
    /// </summary>
    void Enqueue(string message);

    /// <summary>
    /// Le nb de messages qui attendent le cooldown
    /// </summary>
    int PendingCount { get; }

    /// <summary>
    /// Se termine quand tous les messages en file ont été passés au client
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
