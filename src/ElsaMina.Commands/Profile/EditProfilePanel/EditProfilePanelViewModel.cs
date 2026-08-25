using ElsaMina.Commands.Dolls;
using ElsaMina.Core.Services.Templates;

namespace ElsaMina.Commands.Profile.EditProfilePanel;

public class EditProfilePanelViewModel : LocalizableViewModel
{
    public string BotName { get; set; }
    public string Trigger { get; set; }
    public string RoomId { get; set; }
    public string UserId { get; set; }
    public string CurrentEmoji { get; set; }
    public string CurrentBackgroundColor { get; set; }
    public string CurrentTextColor { get; set; }

    /// <summary>
    /// The user's dolls, in the order they show up on their profile.
    /// </summary>
    public IReadOnlyList<Doll> Dolls { get; set; } = [];
}
