using ElsaMina.Commands.Games.LightsOut;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.LightsOut;

[TestFixture]
public class ToggleLightsOutCommandTest
{
    private IRoomsManager _roomsManager;
    private ILightsOutGameManager _gameManager;
    private IContext _context;
    private IUser _sender;
    private ToggleLightsOutCommand _sut;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<ILightsOutGameManager>();
        _context = Substitute.For<IContext>();
        _sender = Substitute.For<IUser>();
        _sender.UserId.Returns("user1");
        _context.Sender.Returns(_sender);
        _sut = new ToggleLightsOutCommand(_roomsManager, _gameManager);
    }

    [Test]
    public void Test_IsPrivateMessageOnly_ShouldBeTrue()
    {
        Assert.That(_sut.IsPrivateMessageOnly, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenFewerThan3Parts()
    {
        _context.Target.Returns("room1,0");

        await _sut.RunAsync(_context);

        _gameManager.DidNotReceive().GetGame(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenRowIsNotANumber()
    {
        _context.Target.Returns("room1,notanumber,0");

        await _sut.RunAsync(_context);

        _gameManager.DidNotReceive().GetGame(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenColIsNotANumber()
    {
        _context.Target.Returns("room1,0,notanumber");

        await _sut.RunAsync(_context);

        _gameManager.DidNotReceive().GetGame(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenNoGameFound()
    {
        _context.Target.Returns("room1,0,0");
        _gameManager.GetGame("room1", "user1").Returns((ILightsOutGame)null);
        var room = Substitute.For<IRoom>();
        room.Game = null;
        _roomsManager.GetRoom("room1").Returns(room);

        await _sut.RunAsync(_context);

        // No exception, no calls
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallToggleCell_WhenGameFoundViaGameManager()
    {
        _context.Target.Returns("room1,2,3");
        var game = Substitute.For<ILightsOutGame>();
        game.IsPrivateMode.Returns(false);
        _gameManager.GetGame("room1", "user1").Returns(game);

        await _sut.RunAsync(_context);

        await game.Received(1).ToggleCell(_sender, 2, 3);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallToggleCell_WhenGameFoundViaRoomFallback()
    {
        _context.Target.Returns("room1,1,4");
        _gameManager.GetGame("room1", "user1").Returns((ILightsOutGame)null);
        var game = Substitute.For<ILightsOutGame>();
        game.IsPrivateMode.Returns(false);
        var room = Substitute.For<IRoom>();
        room.Game.Returns(game);
        _roomsManager.GetRoom("room1").Returns(room);

        await _sut.RunAsync(_context);

        await game.Received(1).ToggleCell(_sender, 1, 4);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUpdateContextAndCulture_WhenPrivateMode()
    {
        _context.Target.Returns("room1,0,0");
        var game = Substitute.For<ILightsOutGame>();
        game.IsPrivateMode.Returns(true);
        _gameManager.GetGame("room1", "user1").Returns(game);
        var room = Substitute.For<IRoom>();
        _roomsManager.GetRoom("room1").Returns(room);

        await _sut.RunAsync(_context);

        Assert.That(game.Context, Is.SameAs(_context));
        await game.Received(1).ToggleCell(_sender, 0, 0);
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimParts_WhenPartsHaveWhitespace()
    {
        _context.Target.Returns(" room1 , 2 , 3 ");
        var game = Substitute.For<ILightsOutGame>();
        game.IsPrivateMode.Returns(false);
        _gameManager.GetGame("room1", "user1").Returns(game);

        await _sut.RunAsync(_context);

        await game.Received(1).ToggleCell(_sender, 2, 3);
    }
}
