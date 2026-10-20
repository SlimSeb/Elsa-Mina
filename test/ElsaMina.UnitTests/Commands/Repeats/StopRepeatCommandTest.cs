using ElsaMina.Commands.Repeats;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Repeats;

public class StopRepeatCommandTest
{
    private static readonly Guid REPEAT_ID = Guid.Parse("8f14e45f-ceea-467a-9b36-6c1b4c3f1a3e");

    private IRepeatsManager _repeatsManager;
    private IContext _context;
    private StopRepeatCommand _command;

    [SetUp]
    public void SetUp()
    {
        var repeat = Substitute.For<IRepeat>();
        repeat.RoomId.Returns("franais");
        _repeatsManager = Substitute.For<IRepeatsManager>();
        _repeatsManager.GetRepeat(Arg.Any<Guid>()).ReturnsNull();
        _repeatsManager.GetRepeat(REPEAT_ID).Returns(repeat);
        _context = Substitute.For<IContext>();
        _context.IsPrivateMessage.Returns(true);
        _command = new StopRepeatCommand(_repeatsManager);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        // Assert
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_GetTargetRoomId_ShouldReturnRoomOfTheRepeat()
    {
        // Arrange
        _context.Target.Returns(REPEAT_ID.ToString());

        // Act & Assert
        Assert.That(_command.GetTargetRoomId(_context), Is.EqualTo("franais"));
    }

    [Test]
    [TestCase("not a guid")]
    [TestCase("00000000-0000-0000-0000-000000000001")]
    public void Test_GetTargetRoomId_ShouldReturnNull_WhenRepeatDoesNotExist(string target)
    {
        // Arrange
        _context.Target.Returns(target);

        // Act & Assert
        Assert.That(_command.GetTargetRoomId(_context), Is.Null);
    }

    [Test]
    public async Task Test_RunAsync_ShouldStopRepeat_WhenItExists()
    {
        // Arrange
        _context.Target.Returns(REPEAT_ID.ToString());
        _repeatsManager.StopRepeatAsync(REPEAT_ID, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("repeat_stop_success");
    }
}
