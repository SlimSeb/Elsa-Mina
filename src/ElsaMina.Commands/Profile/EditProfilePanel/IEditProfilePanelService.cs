using ElsaMina.Core.Contexts;

namespace ElsaMina.Commands.Profile.EditProfilePanel;

public interface IEditProfilePanelService
{
    /// <summary>
    /// Renders the profile edition panel for the sender and sends it as an HTML page. Sending it again
    /// replaces the page the user already has open, which is how the panel refreshes itself after an edit.
    /// </summary>
    Task SendPanelAsync(IContext context, string roomId, CancellationToken cancellationToken = default);
}
