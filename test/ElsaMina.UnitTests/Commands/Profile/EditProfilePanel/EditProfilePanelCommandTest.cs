using ElsaMina.Commands.Profile.EditProfilePanel;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Profile.EditProfilePanel;

public class EditProfilePanelCommandTest
{
    private IEditProfilePanelService _editProfilePanelService;
    private IContext _context;
    private EditProfilePanelCommand _sut;

    [SetUp]
    public void SetUp()
    {
        _editProfilePanelService = Substitute.For<IEditProfilePanelService>();
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("defaultroom");

        _sut = new EditProfilePanelCommand(_editProfilePanelService);
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendThePanelForTheTargetedRoom()
    {
        // Arrange
        _context.Target.Returns(" TestRoom ");

        // Act
        await _sut.RunAsync(_context);

        // Assert
        await _editProfilePanelService.Received(1)
            .SendPanelAsync(_context, "testroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendThePanelForTheCurrentRoom_WhenNoRoomIsGiven()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _sut.RunAsync(_context);

        // Assert
        await _editProfilePanelService.Received(1)
            .SendPanelAsync(_context, "defaultroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_sut.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_sut.IsAllowedInPrivateMessage, Is.True);
    }
}
