using ElsaMina.Commands.Dolls;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.RoomUserData;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Dolls;

public class GiveDollCommandTest
{
    private IContext _context;
    private IDollService _dollService;
    private IRoomUserDataService _roomUserDataService;
    private GiveDollCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _dollService = Substitute.For<IDollService>();
        _roomUserDataService = Substitute.For<IRoomUserDataService>();
        _command = new GiveDollCommand(_dollService, _roomUserDataService);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNotFound_WhenDollIsNotInTheCatalogue()
    {
        // Arrange
        _context.Target.Returns("Alice, UnknownDoll");
        _context.RoomId.Returns("room1");
        _dollService.GetDollAsync("unknowndoll", Arg.Any<CancellationToken>()).Returns((Doll)null);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("doll_not_found", "unknowndoll");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().GiveDollToUserAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithAlreadyOwned_WhenUserAlreadyOwnsTheDoll()
    {
        // Arrange
        _context.Target.Returns("Alice, Pikachu");
        _context.RoomId.Returns("room1");
        _dollService.GetDollAsync("pikachu", Arg.Any<CancellationToken>())
            .Returns(new Doll { Id = "pikachu", Name = "Pikachu", Size = 16, Image = "https://images/pikachu.png" });
        _dollService.IsDollOwnedByUserAsync("room1", "alice", "pikachu", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("doll_give_already_owned", "alice", "Pikachu");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().GiveDollToUserAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldGiveDollAndReplyWithSuccess_WhenArgumentsAreValid()
    {
        // Arrange
        _context.Target.Returns("Alice, Pikachu");
        _context.RoomId.Returns("room1");
        _dollService.GetDollAsync("pikachu", Arg.Any<CancellationToken>())
            .Returns(new Doll { Id = "pikachu", Name = "Pikachu", Size = 16, Image = "https://images/pikachu.png" });
        _dollService.IsDollOwnedByUserAsync("room1", "alice", "pikachu", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _roomUserDataService.Received(1)
            .GiveDollToUserAsync("room1", "alice", "pikachu", Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("doll_give_success", "alice", "Pikachu");
    }

    [Test]
    public async Task Test_RunAsync_ShouldAskForARoom_WhenInPrivateMessageWithoutRoom()
    {
        // Arrange
        _context.Target.Returns("Alice, Pikachu");
        _context.IsPrivateMessage.Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("doll_pm_missing_room");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().GiveDollToUserAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldRefuse_WhenRankIsInsufficientInTheTargetRoom()
    {
        // Arrange
        _context.Target.Returns("Alice, Pikachu, room1");
        _context.IsPrivateMessage.Returns(true);
        _context.HasSufficientRankInRoom("room1", Rank.Driver, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("doll_pm_insufficient_rank");
        await _roomUserDataService.DidNotReceiveWithAnyArgs().GiveDollToUserAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTheRoomArgument_WhenInPrivateMessage()
    {
        // Arrange
        _context.Target.Returns("Alice, Pikachu, Room 1");
        _context.IsPrivateMessage.Returns(true);
        _context.HasSufficientRankInRoom("room1", Rank.Driver, Arg.Any<CancellationToken>()).Returns(true);
        _dollService.GetDollAsync("pikachu", Arg.Any<CancellationToken>())
            .Returns(new Doll { Id = "pikachu", Name = "Pikachu", Size = 16, Image = "https://images/pikachu.png" });

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _roomUserDataService.Received(1)
            .GiveDollToUserAsync("room1", "alice", "pikachu", Arg.Any<CancellationToken>());
    }
}
