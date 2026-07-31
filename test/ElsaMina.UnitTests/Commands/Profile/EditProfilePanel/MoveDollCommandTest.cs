using ElsaMina.Commands.Dolls;
using ElsaMina.Commands.Profile.EditProfilePanel;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Profile.EditProfilePanel;

public class MoveDollCommandTest
{
    private IDollService _dollService;
    private IEditProfilePanelService _editProfilePanelService;
    private IContext _context;
    private MoveDollCommand _sut;

    [SetUp]
    public void SetUp()
    {
        _dollService = Substitute.For<IDollService>();
        _editProfilePanelService = Substitute.For<IEditProfilePanelService>();

        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("alice");

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("room1");
        _context.Sender.Returns(sender);

        _sut = new MoveDollCommand(_dollService, _editProfilePanelService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_sut.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public async Task Test_RunAsync_ShouldMoveTheDollLeftAndRefreshThePanel()
    {
        // Arrange
        _context.Target.Returns("Pikachu, left");
        _dollService.MoveDollAsync("room1", "alice", "pikachu", -1, Arg.Any<CancellationToken>())
            .Returns(DollMoveResult.Moved);

        // Act
        await _sut.RunAsync(_context);

        // Assert
        await _editProfilePanelService.Received(1)
            .SendPanelAsync(_context, "room1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldMoveTheDollRight_WhenTheDirectionIsRight()
    {
        // Arrange
        _context.Target.Returns("Pikachu, right");
        _dollService.MoveDollAsync("room1", "alice", "pikachu", 1, Arg.Any<CancellationToken>())
            .Returns(DollMoveResult.Moved);

        // Act
        await _sut.RunAsync(_context);

        // Assert
        await _dollService.Received(1)
            .MoveDollAsync("room1", "alice", "pikachu", 1, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTheRoomFromTheArguments_WhenSentInPrivateMessage()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("Pikachu, left, Room1");
        _dollService.MoveDollAsync("room1", "alice", "pikachu", -1, Arg.Any<CancellationToken>())
            .Returns(DollMoveResult.Moved);

        // Act
        await _sut.RunAsync(_context);

        // Assert
        await _editProfilePanelService.Received(1)
            .SendPanelAsync(_context, "room1", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldAskForTheRoom_WhenSentInPrivateMessageWithoutOne()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("Pikachu, left");

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("move_doll_missing_room");
        await _dollService.DidNotReceiveWithAnyArgs()
            .MoveDollAsync(default, default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithTheHelpMessage_WhenTheDirectionIsUnknown()
    {
        // Arrange
        _context.Target.Returns("Pikachu, sideways");
        _context.GetString("move_doll_help_message").Returns("help");

        // Act
        await _sut.RunAsync(_context);

        // Assert
        await _dollService.DidNotReceiveWithAnyArgs()
            .MoveDollAsync(default, default, default, default);
        await _editProfilePanelService.DidNotReceiveWithAnyArgs()
            .SendPanelAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNotOwned_AndNotRefreshThePanel_WhenTheDollIsNotOwned()
    {
        // Arrange
        _context.Target.Returns("Pikachu, left");
        _dollService.MoveDollAsync("room1", "alice", "pikachu", -1, Arg.Any<CancellationToken>())
            .Returns(DollMoveResult.NotOwned);

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("move_doll_not_owned", "pikachu");
        await _editProfilePanelService.DidNotReceiveWithAnyArgs()
            .SendPanelAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithAtEdge_WhenTheDollCannotMoveFurther()
    {
        // Arrange
        _context.Target.Returns("Pikachu, left");
        _dollService.MoveDollAsync("room1", "alice", "pikachu", -1, Arg.Any<CancellationToken>())
            .Returns(DollMoveResult.AlreadyAtEdge);

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("move_doll_at_edge");
        await _editProfilePanelService.DidNotReceiveWithAnyArgs()
            .SendPanelAsync(default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithFailure_WhenTheCatalogueCannotBeRead()
    {
        // Arrange
        _context.Target.Returns("Pikachu, left");
        _dollService.MoveDollAsync("room1", "alice", "pikachu", -1, Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("drive down"));

        // Act
        await _sut.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("move_doll_failure", "drive down");
        await _editProfilePanelService.DidNotReceiveWithAnyArgs()
            .SendPanelAsync(default, default);
    }
}
