using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;

namespace ElsaMina.Core.Services.Commands;

public interface ICommand
{
    public string Name { get; }
    public IEnumerable<string> Aliases { get; }
    public string Category { get; }
    public bool IsAllowedInPrivateMessage { get; }
    public bool IsWhitelistOnly { get; }
    public bool IsPrivateMessageOnly { get; }
    public Rank RequiredRank { get; }
    public string HelpMessageKey { get; }
    public bool IsHidden { get; }
    public IEnumerable<string> RoomRestriction { get; }

    /// <summary>
    /// Si true, la commande passe dans l'ordre avec les autres messages de sa room : le message suivant de la room est
    /// traité seulement une fois la commande finie. A utiliser pour les commandes qui modifient un état en mémoire partagé
    /// avec d'autres messages (genre un jeu). Surtout pas pour une commande qui attend un message du serveur, sinon elle
    /// attend un message coincé derrière elle-même ^^ Si false (par défaut), la commande tourne en fond.
    /// </summary>
    public bool RunsInMessageOrder { get; }

    Task RunAsync(IContext context, CancellationToken cancellationToken = default);
}