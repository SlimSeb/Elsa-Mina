using ElsaMina.Core.Services.Commands;

namespace ElsaMina.Commands.Games;

/// <summary>
/// Classe de base pour les commandes qui modifient l'état en mémoire d'un jeu. Elles passent dans l'ordre avec les
/// autres messages de la room (cf <see cref="ICommand.RunsInMessageOrder"/>), comme ça deux coups (ou un coup + une
/// réponse dans le chat lue par un handler du jeu) se mélangent jamais
/// </summary>
public abstract class GameCommand : Command
{
    public override bool RunsInMessageOrder => true;
}
