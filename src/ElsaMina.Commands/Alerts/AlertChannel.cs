namespace ElsaMina.Commands.Alerts;

/// <param name="ChannelId">Identifiant stable utilisé pour interroger la plateforme.</param>
/// <param name="ChannelName">Nom lisible (login, handle, pseudo) utilisé pour l'affichage et la suppression.</param>
public record AlertChannel(string ChannelId, string ChannelName);
