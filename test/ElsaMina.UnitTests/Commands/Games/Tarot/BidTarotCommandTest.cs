using ElsaMina.Commands.Games.Tarot;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Tarot;

[TestFixture]
public class BidTarotCommandTest
{
    private IRoomsManager _roomsManager;
    private BidTarotCommand _command;
    private IContext _context;
    private IRoom _room;
    private ITarotGame _game;
    private IUser _sender;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _game = Substitute.For<ITarotGame>();
        _sender = Substitute.For<IUser>();

        _room.Game.Returns(_game);
        _context.Sender.Returns(_sender);

        _command = new BidTarotCommand(_roomsManager);
    }

    [Test]
    public async Task Test_RunAsync_ShouldBid_WhenTypedInRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.Room.Returns(_room);
        _context.Target.Returns("garde");

        await _command.RunAsync(_context);

        await _game.Received(1).BidAsync(_sender, TarotBid.Garde);
    }

    [Test]
    public async Task Test_RunAsync_ShouldBid_WhenSentFromButtonAsPrivateMessage()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("testroom, gardesans");
        _roomsManager.GetRoom("testroom").Returns(_room);

        await _command.RunAsync(_context);

        await _game.Received(1).BidAsync(_sender, TarotBid.GardeSans);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyUnknownBid_WhenBidIsInvalid()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.Room.Returns(_room);
        _context.Target.Returns("notabid");

        await _command.RunAsync(_context);

        using (Assert.EnterMultipleScope())
        {
            _context.Received(1).ReplyLocalizedMessage("tarot_bid_unknown");
            await _game.DidNotReceive().BidAsync(Arg.Any<IUser>(), Arg.Any<TarotBid>());
        }
    }
}
