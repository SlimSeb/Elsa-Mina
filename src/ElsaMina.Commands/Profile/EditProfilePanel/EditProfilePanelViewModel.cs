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
}
