using ElsaMina.Commands.Games.GuessingGame;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.GuessingGame;

[TestFixture]
public class GuessingGameAnswerCommandTest
{
    private IRoomsManager _roomsManager;
    private IContext _context;
    private IUser _sender;
    private GuessingGameAnswerCommand _command;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _context = Substitute.For<IContext>();
        _sender = Substitute.For<IUser>();
        _sender.Name.Returns("TestUser");
        _context.Sender.Returns(_sender);
        _command = new GuessingGameAnswerCommand(_roomsManager);
    }

    [Test]
    public void Test_IsPrivateMessageOnly_ShouldBeTrue()
    {
        Assert.That(_command.IsPrivateMessageOnly, Is.True);
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public void Test_RunAsync_ShouldDoNothing_WhenTargetHasNoComma()
    {
        _context.Target.Returns("noanswer");

        _command.RunAsync(_context);

        _roomsManager.DidNotReceive().GetRoom(Arg.Any<string>());
    }

    [Test]
    public void Test_RunAsync_ShouldDoNothing_WhenRoomNotFound()
    {
        _context.Target.Returns("testroom,France");
        _roomsManager.GetRoom("testroom").Returns((IRoom)null);

        _command.RunAsync(_context);

        _roomsManager.Received(1).GetRoom("testroom");
    }

    [Test]
    public void Test_RunAsync_ShouldDoNothing_WhenRoomHasNoGuessingGame()
    {
        var room = Substitute.For<IRoom>();
        room.Game.Returns((IGame)null);
        _context.Target.Returns("testroom,France");
        _roomsManager.GetRoom("testroom").Returns(room);

        _command.RunAsync(_context);

        // No exception - just silently ignored
        _roomsManager.Received(1).GetRoom("testroom");
    }

    [Test]
    public void Test_RunAsync_ShouldCallOnAnswer_WhenRoomHasGuessingGame()
    {
        var guessingGame = Substitute.For<IGuessingGame, IGame>();
        var room = Substitute.For<IRoom>();
        room.Game.Returns((IGame)guessingGame);
        _context.Target.Returns("testroom,France");
        _roomsManager.GetRoom("testroom").Returns(room);

        _command.RunAsync(_context);

        guessingGame.Received(1).OnAnswer("TestUser", "France");
    }

    [Test]
    public void Test_RunAsync_ShouldNormalizeRoomId_WhenLookingUpRoom()
    {
        _context.Target.Returns("Test Room,France");
        _roomsManager.GetRoom("testroom").Returns((IRoom)null);

        _command.RunAsync(_context);

        _roomsManager.Received(1).GetRoom("testroom");
    }

    [Test]
    public void Test_RunAsync_ShouldDoNothing_WhenTargetHasMoreThanOneComma()
    {
        _context.Target.Returns("testroom,part1,part2");

        _command.RunAsync(_context);

        _roomsManager.DidNotReceive().GetRoom(Arg.Any<string>());
    }
}
