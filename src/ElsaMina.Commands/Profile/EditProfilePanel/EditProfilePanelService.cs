using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;

namespace ElsaMina.Commands.Profile.EditProfilePanel;

public class EditProfilePanelService : IEditProfilePanelService
{
    private const string TEMPLATE_KEY = "Profile/EditProfilePanel/EditProfilePanel";

    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IRoomsManager _roomsManager;
    private readonly IProfileService _profileService;

    public EditProfilePanelService(ITemplatesManager templatesManager,
        IConfiguration configuration,
        IRoomsManager roomsManager,
        IProfileService profileService)
    {
        _templatesManager = templatesManager;
        _configuration = configuration;
        _roomsManager = roomsManager;
        _profileService = profileService;
    }

    public async Task SendPanelAsync(IContext context, string roomId, CancellationToken cancellationToken = default)
    {
        var room = _roomsManager.GetRoom(roomId);
        if (context.IsPrivateMessage && room != null)
        {
            context.Culture = room.Culture;
        }

        var userId = context.Sender.UserId;
        var profileViewModel = await _profileService.GetProfileViewModelAsync(userId, roomId, cancellationToken);
        if (context.Culture != null)
        {
            profileViewModel.Culture = context.Culture;
        }

        var viewModel = new EditProfilePanelViewModel
        {
            Culture = context.Culture,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = roomId,
            UserId = userId,
            CurrentEmoji = profileViewModel.ProfileEmoji ?? string.Empty,
            CurrentBackgroundColor = profileViewModel.ProfileBackgroundColor ?? string.Empty,
            CurrentTextColor = profileViewModel.ProfileTextColor ?? string.Empty,
            CurrentLabelColor = profileViewModel.ProfileLabelColor ?? string.Empty,
            Dolls = profileViewModel.Dolls?.ToList() ?? [],
            ProfilePreview = profileViewModel
        };

        var template = await _templatesManager.GetTemplateAsync(TEMPLATE_KEY, viewModel);
        context.ReplyHtmlPage($"edit-profile-{userId}", template
            .RemoveNewlines()
            .CollapseAttributeWhitespace()
            .RemoveWhitespacesBetweenTags());
    }
}
