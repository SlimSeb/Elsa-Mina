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

    /// <summary>
    /// Le rang demandé quand la commande est lancée en PM, dans la room de <see cref="GetTargetRoomId"/>. Par défaut le
    /// même que <see cref="RequiredRank"/>. Mettre Regular pour une commande ouverte à tous en PM (une info, un jeu solo) :
    /// en room son rang sert juste à éviter le spam
    /// </summary>
    public Rank RequiredRankInPrivateMessage { get; }

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

    /// <summary>
    /// La room où on vérifie le rang du user avant de lancer la commande. Par défaut celle du contexte (la room par
    /// défaut en PM). Une commande qui agit sur une room passée en argument doit la renvoyer ici, avec le même parsing que
    /// <see cref="RunAsync"/>, sinon on vérifie le rang dans une room et elle agit dans une autre. Null = la room du
    /// contexte, à renvoyer seulement si la commande agit alors dans cette room ou refuse de tourner
    /// </summary>
    string GetTargetRoomId(IContext context);

    Task RunAsync(IContext context, CancellationToken cancellationToken = default);
}