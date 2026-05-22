using ElsaMina.Commands.Games.LightsOut;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.LightsOut;

[TestFixture]
public class JoinLightsOutCommandTest
{
    private IRoomsManager _roomsManager;
    private IContext _context;
    private JoinLightsOutCommand _sut;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _context = Substitute.For<IContext>();
        _sut = new JoinLightsOutCommand(_roomsManager);
    }

    [Test]
    public void Test_IsPrivateMessageOnly_ShouldBeTrue()
    {
        Assert.That(_sut.IsPrivateMessageOnly, Is.True);
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_sut.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenRoomNotFound()
    {
        _context.Target.Returns("unknownroom");
        _roomsManager.GetRoom("unknownroom").Returns((IRoom)null);

        await _sut.RunAsync(_context);

        // No exception and no interaction with a game
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenRoomHasNoGame()
    {
        _context.Target.Returns("room1");
        var room = Substitute.For<IRoom>();
        room.Game = null;
        _roomsManager.GetRoom("room1").Returns(room);

        await _sut.RunAsync(_context);

        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenGameIsNotLightsOut()
    {
        _context.Target.Returns("room1");
        var room = Substitute.For<IRoom>();
        room.Game.Returns(Substitute.For<ElsaMina.Core.Services.Games.IGame>());
        _roomsManager.GetRoom("room1").Returns(room);

        await _sut.RunAsync(_context);

        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenGameIsAlreadyStarted()
    {
        _context.Target.Returns("room1");
        var room = Substitute.For<IRoom>();
        var game = Substitute.For<ILightsOutGame>();
        game.IsStarted.Returns(true);
        room.Game.Returns(game);
        _roomsManager.GetRoom("room1").Returns(room);

        await _sut.RunAsync(_context);

        await game.DidNotReceive().StartNewRound();
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetOwnerAndStartRound_WhenGameWaiting()
    {
        var sender = Substitute.For<IUser>();
        _context.Target.Returns("room1");
        _context.Sender.Returns(sender);
        var room = Substitute.For<IRoom>();
        var game = Substitute.For<ILightsOutGame>();
        game.IsStarted.Returns(false);
        room.Game.Returns(game);
        _roomsManager.GetRoom("room1").Returns(room);

        await _sut.RunAsync(_context);

        Assert.That(game.Owner, Is.SameAs(sender));
        await game.Received(1).StartNewRound();
    }
}
