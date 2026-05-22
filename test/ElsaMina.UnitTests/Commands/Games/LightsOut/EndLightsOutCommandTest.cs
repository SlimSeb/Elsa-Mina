using ElsaMina.Commands.Games.LightsOut;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.LightsOut;

[TestFixture]
public class EndLightsOutCommandTest
{
    private IRoomsManager _roomsManager;
    private ILightsOutGameManager _gameManager;
    private IContext _context;
    private IUser _sender;
    private EndLightsOutCommand _sut;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<ILightsOutGameManager>();
        _context = Substitute.For<IContext>();
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("user1");
        _context.Sender.Returns(_sender);
        _sut = new EndLightsOutCommand(_roomsManager, _gameManager);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_sut.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_sut.IsAllowedInPrivateMessage, Is.True);
    }

    // --- Private message path ---

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenPmWithEmptyTarget()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns(string.Empty);

        await _sut.RunAsync(_context);

        _gameManager.DidNotReceive().GetGame(Arg.Any<string>(), Arg.Any<string>());
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoGame_WhenPmAndNoGameFound()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("room1");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());
        _gameManager.GetGame("room1", "user1").Returns((ILightsOutGame)null);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("lo_game_no_game");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelAndReplyCancelled_WhenPmAndGameFound()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("room1");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());
        var game = Substitute.For<ILightsOutGame>();
        _gameManager.GetGame("room1", "user1").Returns(game);

        await _sut.RunAsync(_context);

        await game.Received(1).CancelAsync();
        _context.Received(1).ReplyLocalizedMessage("lo_game_cancelled");
    }

    [Test]
    public async Task Test_RunAsync_ShouldUpdateContextBeforeCancel_WhenPm()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("room1");
        _roomsManager.GetRoom("room1").Returns(Substitute.For<IRoom>());
        var game = Substitute.For<ILightsOutGame>();
        _gameManager.GetGame("room1", "user1").Returns(game);

        await _sut.RunAsync(_context);

        Assert.That(game.Context, Is.SameAs(_context));
    }

    // --- Room message path ---

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoGame_WhenRoomHasNoGame()
    {
        _context.IsPrivateMessage.Returns(false);
        var room = Substitute.For<IRoom>();
        room.Game = null;
        _context.Room.Returns(room);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("lo_game_no_game");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoGame_WhenRoomGameIsNotLightsOut()
    {
        _context.IsPrivateMessage.Returns(false);
        var room = Substitute.For<IRoom>();
        room.Game.Returns(Substitute.For<ElsaMina.Core.Services.Games.IGame>());
        _context.Room.Returns(room);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("lo_game_no_game");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelAndReplyCancelled_WhenOwnerEndsGame()
    {
        _context.IsPrivateMessage.Returns(false);
        var owner = Substitute.For<IUser>();
        owner.UserId.Returns("user1");
        _sender.UserId.Returns("user1");
        var game = Substitute.For<ILightsOutGame>();
        game.Owner.Returns(owner);
        var room = Substitute.For<IRoom>();
        room.Game.Returns(game);
        _context.Room.Returns(room);

        await _sut.RunAsync(_context);

        await game.Received(1).CancelAsync();
        _context.Received(1).ReplyLocalizedMessage("lo_game_cancelled");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelAndReplyCancelled_WhenDriverEndsOthersGame()
    {
        _context.IsPrivateMessage.Returns(false);
        var owner = Substitute.For<IUser>();
        owner.UserId.Returns("owneruser");
        _sender.UserId.Returns("moderator");
        _context.HasRankOrHigher(Rank.Driver).Returns(true);
        var game = Substitute.For<ILightsOutGame>();
        game.Owner.Returns(owner);
        var room = Substitute.For<IRoom>();
        room.Game.Returns(game);
        _context.Room.Returns(room);

        await _sut.RunAsync(_context);

        await game.Received(1).CancelAsync();
        _context.Received(1).ReplyLocalizedMessage("lo_game_cancelled");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotOwner_WhenNonOwnerLowRankTriesToEnd()
    {
        _context.IsPrivateMessage.Returns(false);
        var owner = Substitute.For<IUser>();
        owner.UserId.Returns("owneruser");
        _sender.UserId.Returns("otheruser");
        _context.HasRankOrHigher(Rank.Driver).Returns(false);
        var game = Substitute.For<ILightsOutGame>();
        game.Owner.Returns(owner);
        var room = Substitute.For<IRoom>();
        room.Game.Returns(game);
        _context.Room.Returns(room);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("lo_game_not_owner");
        await game.DidNotReceive().CancelAsync();
    }

    [Test]
    public async Task Test_RunAsync_ShouldCancelGame_WhenOwnerIsNull()
    {
        _context.IsPrivateMessage.Returns(false);
        var game = Substitute.For<ILightsOutGame>();
        game.Owner.Returns((IUser)null);
        var room = Substitute.For<IRoom>();
        room.Game.Returns(game);
        _context.Room.Returns(room);

        await _sut.RunAsync(_context);

        await game.Received(1).CancelAsync();
    }
}
