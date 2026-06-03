using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Commands.Games.Semantix;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.DependencyInjection;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Games.Semantix;

[TestFixture]
public class StartSemantixCommandTest
{
    private IDependencyContainerService _dependencyContainerService;
    private IRoomsManager _roomsManager;
    private ISemantixGameManager _gameManager;
    private ISemantixDailyService _dailyService;
    private IArcadeEventsService _arcadeEventsService;
    private IContext _context;
    private StartSemantixCommand _sut;

    [SetUp]
    public void SetUp()
    {
        _dependencyContainerService = Substitute.For<IDependencyContainerService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _gameManager = Substitute.For<ISemantixGameManager>();
        _dailyService = Substitute.For<ISemantixDailyService>();
        _arcadeEventsService = Substitute.For<IArcadeEventsService>();
        _context = Substitute.For<IContext>();

        _gameManager.GetGame(Arg.Any<string>(), Arg.Any<string>()).ReturnsNull();

        _sut = new StartSemantixCommand(_dependencyContainerService, _roomsManager, _gameManager,
            _dailyService, _arcadeEventsService);
    }

    [Test]
    public void Test_Properties_ShouldBeConfigured()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_sut.Name, Is.EqualTo("semantix"));
            Assert.That(_sut.RequiredRank, Is.EqualTo(Rank.Voiced));
            Assert.That(_sut.IsAllowedInPrivateMessage, Is.True);
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyMissingRoom_WhenPmWithoutTarget()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns(string.Empty);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("sx_pm_missing_room");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidRoom_WhenPmRoomUnknown()
    {
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("unknownroom");
        _roomsManager.GetRoom("unknownroom").ReturnsNull();

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("sx_pm_invalid_room");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyAlreadyWon_WhenUserWonToday()
    {
        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("user1");
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("room");
        _context.Sender.Returns(sender);
        _dailyService.HasWonTodayAsync("user1", Arg.Any<CancellationToken>()).Returns(true);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("sx_already_won_today");
        _dependencyContainerService.DidNotReceive().Resolve<SemantixGame>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldResumeExistingGame_WhenGameIsNotEnded()
    {
        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("user1");
        var existingGame = Substitute.For<ISemantixGame>();
        existingGame.IsEnded.Returns(false);

        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("room");
        _context.Sender.Returns(sender);
        _gameManager.GetGame("room", "user1").Returns(existingGame);

        await _sut.RunAsync(_context);

        await existingGame.Received(1).ResumeAsync();
        _dependencyContainerService.DidNotReceive().Resolve<SemantixGame>();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyMuted_WhenGamesAreMutedInRoom()
    {
        _context.IsPrivateMessage.Returns(false);
        _context.RoomId.Returns("room");
        _arcadeEventsService.AreGamesMuted("room").Returns(true);

        await _sut.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("games_muted_event");
    }
}
