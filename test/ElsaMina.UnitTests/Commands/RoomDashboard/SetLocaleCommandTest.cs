using System.Globalization;
using ElsaMina.Commands.RoomDashboard;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.RoomDashboard;

[TestFixture]
public class SetLocaleCommandTest
{
    private IContext _context;
    private IRoomsManager _roomsManager;
    private SetLocaleCommand _command;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _command = new SetLocaleCommand(_roomsManager);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRoomOwner()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.RoomOwner));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidLocale_WhenLocaleIsUnknown()
    {
        // Locales with spaces/special characters are reliably rejected by ICU on all platforms
        _context.Target.Returns("testroom, not a valid locale!");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("setlocale_invalid_locale", Arg.Any<object[]>());
        _roomsManager.DidNotReceive().GetRoom(Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomNotFound_WhenRoomDoesNotExist()
    {
        _context.Target.Returns("unknownroom, fr-FR");
        _roomsManager.GetRoom("unknownroom").Returns((IRoom)null);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("setlocale_room_not_found", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFailure_WhenSetParameterValueReturnsFalse()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom, fr-FR");
        _roomsManager.GetRoom("testroom").Returns(room);
        room.SetParameterValueAsync(Parameter.Locale, "fr-FR", Arg.Any<CancellationToken>())
            .Returns(false);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("setlocale_failure");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplySuccess_WhenSetParameterValueReturnsTrue()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom, fr-FR");
        _roomsManager.GetRoom("testroom").Returns(room);
        room.SetParameterValueAsync(Parameter.Locale, "fr-FR", Arg.Any<CancellationToken>())
            .Returns(true);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("setlocale_success", Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetContextCulture_WhenLocaleIsValid()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("testroom, fr-FR");
        _roomsManager.GetRoom("testroom").Returns(room);
        room.SetParameterValueAsync(Arg.Any<Parameter>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _command.RunAsync(_context);

        Assert.That(_context.Culture.Name, Is.EqualTo("fr-FR"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimWhitespace_WhenArgumentsHaveExtraSpaces()
    {
        var room = Substitute.For<IRoom>();
        _context.Target.Returns("  testroom  ,  fr-FR  ");
        _roomsManager.GetRoom("testroom").Returns(room);
        room.SetParameterValueAsync(Arg.Any<Parameter>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _command.RunAsync(_context);

        _roomsManager.Received(1).GetRoom("testroom");
        await room.Received(1).SetParameterValueAsync(Parameter.Locale, "fr-FR", Arg.Any<CancellationToken>());
    }

    [Test]
    [TestCase(false, "otherroom, fr-FR", ExpectedResult = "otherroom")]
    [TestCase(false, " otherroom ", ExpectedResult = "otherroom")]
    public string Test_GetTargetRoomId_ShouldReturnFirstArgument(bool isPrivateMessage, string target)
    {
        // Arrange
        _context.IsPrivateMessage.Returns(isPrivateMessage);
        _context.Target.Returns(target);
        _context.RoomId.Returns("contextroom");

        // Act & Assert
        return _command.GetTargetRoomId(_context);
    }
}
