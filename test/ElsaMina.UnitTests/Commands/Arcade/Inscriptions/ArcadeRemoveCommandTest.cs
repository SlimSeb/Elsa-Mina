using ElsaMina.Commands.Arcade.Inscriptions;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Inscriptions;

[TestFixture]
public class ArcadeRemoveCommandTest
{
    private IArcadeInscriptionsManager _inscriptionsManager;
    private IContext _context;
    private IRoom _room;
    private ArcadeRemoveCommand _command;

    [SetUp]
    public void SetUp()
    {
        _inscriptionsManager = Substitute.For<IArcadeInscriptionsManager>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _context.RoomId.Returns("testroom");
        _context.Room.Returns(_room);
        _room.Users.Returns(new Dictionary<string, IUser>());
        _command = new ArcadeRemoveCommand(_inscriptionsManager);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeCorrect()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("arcade_remove_help"));
    }

    [Test]
    public void Test_RunAsync_ShouldReplyHelp_WhenTargetIsEmpty()
    {
        _context.Target.Returns(string.Empty);

        _command.RunAsync(_context);

        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public void Test_RunAsync_ShouldReplyNoData_WhenNoInscriptionsExist()
    {
        _context.Target.Returns("someuser");
        ArcadeRoomState dummy = null;
        _inscriptionsManager.TryGetState("testroom", out dummy).Returns(false);

        _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("arcade_list_no_data");
    }

    [Test]
    public void Test_RunAsync_ShouldReplyNotRegistered_WhenUserIsNotParticipant()
    {
        _context.Target.Returns("unknownuser");
        var state = new ArcadeRoomState { IsActive = true, Title = "Test" };
        ArcadeRoomState dummy = null;
        _inscriptionsManager.TryGetState("testroom", out dummy)
            .Returns(callInfo => { callInfo[1] = state; return true; });

        _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("arcade_remove_not_registered", "unknownuser");
    }

    [Test]
    public void Test_RunAsync_ShouldRemoveParticipantAndBan_WhenUserIsRegistered()
    {
        _context.Target.Returns("someuser");
        var state = new ArcadeRoomState { IsActive = true, Title = "Test" };
        state.Participants.Add("someuser");
        state.Participants.Add("player2");
        state.Participants.Add("player3");
        ArcadeRoomState dummy = null;
        _inscriptionsManager.TryGetState("testroom", out dummy)
            .Returns(callInfo => { callInfo[1] = state; return true; });

        _command.RunAsync(_context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(state.Participants, Does.Not.Contain("someuser"));
            Assert.That(state.BannedUsers, Contains.Item("someuser"));
        }
        _context.Received(1).ReplyLocalizedMessage("arcade_remove_success", "someuser");
    }

    [Test]
    public void Test_RunAsync_ShouldReplyHtmlWithWinner_WhenOneParticipantRemains()
    {
        _context.Target.Returns("loser");
        var state = new ArcadeRoomState { IsActive = true, Title = "Test" };
        state.Participants.Add("loser");
        state.Participants.Add("winner");
        ArcadeRoomState dummy = null;
        _inscriptionsManager.TryGetState("testroom", out dummy)
            .Returns(callInfo => { callInfo[1] = state; return true; });

        _command.RunAsync(_context);

        Assert.That(state.IsActive, Is.False);
        _context.Received(1).ReplyHtml(Arg.Is<string>(html => html.Contains("winner")));
    }

    [Test]
    public void Test_RunAsync_ShouldReplyHtmlWithNoParticipants_WhenNoParticipantsRemain()
    {
        _context.Target.Returns("lastuser");
        var state = new ArcadeRoomState { IsActive = true, Title = "Test" };
        state.Participants.Add("lastuser");
        ArcadeRoomState dummy = null;
        _inscriptionsManager.TryGetState("testroom", out dummy)
            .Returns(callInfo => { callInfo[1] = state; return true; });

        _command.RunAsync(_context);

        Assert.That(state.IsActive, Is.False);
        _context.Received(1).ReplyHtml(Arg.Any<string>());
    }
}
