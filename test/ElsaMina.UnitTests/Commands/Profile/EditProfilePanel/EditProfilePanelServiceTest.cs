using System.Globalization;
using ElsaMina.Commands.Dolls;
using ElsaMina.Commands.Profile;
using ElsaMina.Commands.Profile.EditProfilePanel;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Profile.EditProfilePanel;

public class EditProfilePanelServiceTest
{
    private const string TEMPLATE_KEY = "Profile/EditProfilePanel/EditProfilePanel";

    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IRoomsManager _roomsManager;
    private IProfileService _profileService;
    private IContext _context;
    private EditProfilePanelService _sut;

    [SetUp]
    public void SetUp()
    {
        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<EditProfilePanelViewModel>())
            .Returns("<div>panel</div>");

        _configuration = Substitute.For<IConfiguration>();
        _configuration.Name.Returns("Elsa");
        _configuration.Trigger.Returns("-");

        _roomsManager = Substitute.For<IRoomsManager>();
        _roomsManager.GetRoom(Arg.Any<string>()).Returns((IRoom)null);

        _profileService = Substitute.For<IProfileService>();
        _profileService.GetProfileViewModelAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProfileViewModel());

        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("alice");

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("testroom");
        _context.IsPrivateMessage.Returns(false);
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _context.Sender.Returns(sender);

        _sut = new EditProfilePanelService(_templatesManager, _configuration, _roomsManager, _profileService);
    }

    [Test]
    public async Task Test_SendPanelAsync_ShouldRenderThePanel_WhenTheUserHasNoStoredData()
    {
        // Arrange
        var profileViewModel = new ProfileViewModel
        {
            UserId = "alice",
            UserName = "alice"
        };
        _profileService.GetProfileViewModelAsync("alice", "testroom", Arg.Any<CancellationToken>())
            .Returns(profileViewModel);

        // Act
        await _sut.SendPanelAsync(_context, "testroom");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(TEMPLATE_KEY,
            Arg.Is<EditProfilePanelViewModel>(viewModel =>
                viewModel.RoomId == "testroom" &&
                viewModel.UserId == "alice" &&
                viewModel.BotName == "Elsa" &&
                viewModel.Trigger == "-" &&
                string.IsNullOrEmpty(viewModel.CurrentEmoji) &&
                viewModel.Dolls.Count == 0 &&
                viewModel.ProfilePreview == profileViewModel));
        _context.Received(1).ReplyHtmlPage("edit-profile-alice", "<div>panel</div>");
    }

    [Test]
    public async Task Test_SendPanelAsync_ShouldRenderTheStoredEmojiAndColor()
    {
        // Arrange
        var profileViewModel = new ProfileViewModel
        {
            UserId = "alice",
            ProfileEmoji = "🎮",
            ProfileBackgroundColor = "#8867aa73",
            ProfileTextColor = "#e0d060",
            ProfileLabelColor = "#6ad0d0"
        };
        _profileService.GetProfileViewModelAsync("alice", "testroom", Arg.Any<CancellationToken>())
            .Returns(profileViewModel);

        // Act
        await _sut.SendPanelAsync(_context, "testroom");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(TEMPLATE_KEY,
            Arg.Is<EditProfilePanelViewModel>(viewModel =>
                viewModel.CurrentEmoji == "🎮" &&
                viewModel.CurrentBackgroundColor == "#8867aa73" &&
                viewModel.CurrentTextColor == "#e0d060" &&
                viewModel.CurrentLabelColor == "#6ad0d0" &&
                viewModel.ProfilePreview == profileViewModel));
    }

    [Test]
    public async Task Test_SendPanelAsync_ShouldRenderTheDolls_InTheirShelfOrder()
    {
        // Arrange
        var shelf = new List<Doll>
        {
            new() { Id = "snorlax", Name = "Snorlax", Size = 32, Image = "https://images/snorlax.png" },
            new() { Id = "pikachu", Name = "Pikachu", Size = 16, Image = "https://images/pikachu.png" }
        };
        var profileViewModel = new ProfileViewModel
        {
            UserId = "alice",
            Dolls = shelf
        };
        _profileService.GetProfileViewModelAsync("alice", "testroom", Arg.Any<CancellationToken>())
            .Returns(profileViewModel);

        // Act
        await _sut.SendPanelAsync(_context, "testroom");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(TEMPLATE_KEY,
            Arg.Is<EditProfilePanelViewModel>(viewModel =>
                viewModel.Dolls.SequenceEqual(shelf) &&
                viewModel.ProfilePreview == profileViewModel));
    }

    [Test]
    public async Task Test_SendPanelAsync_ShouldUseTheRoomCulture_WhenCalledFromAPrivateMessage()
    {
        // Arrange
        var room = Substitute.For<IRoom>();
        room.Culture.Returns(new CultureInfo("fr-FR"));
        _roomsManager.GetRoom("testroom").Returns(room);
        _context.IsPrivateMessage.Returns(true);

        var profileViewModel = new ProfileViewModel();
        _profileService.GetProfileViewModelAsync("alice", "testroom", Arg.Any<CancellationToken>())
            .Returns(profileViewModel);

        // Act
        await _sut.SendPanelAsync(_context, "testroom");

        // Assert
        _context.Received().Culture = new CultureInfo("fr-FR");
        Assert.That(profileViewModel.Culture, Is.EqualTo(new CultureInfo("fr-FR")));
    }
}
