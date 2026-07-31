using ElsaMina.Commands.Dolls;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.Core.Utils;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace ElsaMina.Commands.Profile.EditProfilePanel;

public class EditProfilePanelService : IEditProfilePanelService
{
    private const string TEMPLATE_KEY = "Profile/EditProfilePanel/EditProfilePanel";

    private readonly IBotDbContextFactory _dbContextFactory;
    private readonly ITemplatesManager _templatesManager;
    private readonly IConfiguration _configuration;
    private readonly IRoomsManager _roomsManager;
    private readonly IDollService _dollService;

    public EditProfilePanelService(IBotDbContextFactory dbContextFactory,
        ITemplatesManager templatesManager,
        IConfiguration configuration,
        IRoomsManager roomsManager,
        IDollService dollService)
    {
        _dbContextFactory = dbContextFactory;
        _templatesManager = templatesManager;
        _configuration = configuration;
        _roomsManager = roomsManager;
        _dollService = dollService;
    }

    public async Task SendPanelAsync(IContext context, string roomId, CancellationToken cancellationToken = default)
    {
        var room = _roomsManager.GetRoom(roomId);
        if (context.IsPrivateMessage && room != null)
        {
            context.Culture = room.Culture;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var userId = context.Sender.UserId;
        var storedUser = await dbContext.RoomUsers
            .Include(roomUser => roomUser.Dolls)
            .FirstOrDefaultAsync(roomUser => roomUser.Id == userId && roomUser.RoomId == roomId, cancellationToken);

        var viewModel = new EditProfilePanelViewModel
        {
            Culture = context.Culture,
            BotName = _configuration.Name,
            Trigger = _configuration.Trigger,
            RoomId = roomId,
            UserId = userId,
            CurrentEmoji = storedUser?.ProfileEmoji ?? string.Empty,
            CurrentBackgroundColor = storedUser?.ProfileBackgroundColor ?? string.Empty,
            Dolls = await _dollService.ResolveDollsAsync(storedUser?.Dolls ?? [], cancellationToken)
        };

        var template = await _templatesManager.GetTemplateAsync(TEMPLATE_KEY, viewModel);
        context.ReplyHtmlPage($"edit-profile-{userId}", template
            .RemoveNewlines()
            .CollapseAttributeWhitespace()
            .RemoveWhitespacesBetweenTags());
    }
}
