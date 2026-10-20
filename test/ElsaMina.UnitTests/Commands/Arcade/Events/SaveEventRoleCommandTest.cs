using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Events;

[TestFixture]
public class SaveEventRoleCommandTest
{
    private IRoomsManager _roomsManager;
    private IEventRoleMappingService _eventRoleMappingService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private IRoom _room;
    private SaveEventRoleCommand _command;

    [SetUp]
    public void SetUp()
    {
        _roomsManager = Substitute.For<IRoomsManager>();
        _eventRoleMappingService = Substitute.For<IEventRoleMappingService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();

        _configuration.Name.Returns("BotName");
        _configuration.Trigger.Returns("-");

        _eventRoleMappingService.GetMappingsForRoomAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<EventRoleMapping>>([]));

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<EventRoleMappingViewModel>())
            .Returns(Task.FromResult("<html/>"));

        _command = new SaveEventRoleCommand(_roomsManager, _eventRoleMappingService, _templatesManager, _configuration);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeDriver()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Driver));
    }

    [Test]
    public void Test_IsPrivateMessageOnly_ShouldBeTrue()
    {
        Assert.That(_command.IsPrivateMessageOnly, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyMissingData_WhenTargetIsEmpty()
    {
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("eventroles_missing_data");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidFormat_WhenLessThanThreeParts()
    {
        _context.Target.Returns("roomId;;eventName");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("eventroles_invalid_format");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNameRequired_WhenEventNameIsEmpty()
    {
        _context.Target.Returns("roomId;;;;roleId");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("eventroles_name_required");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoleIdRequired_WhenDiscordRoleIdIsEmpty()
    {
        _context.Target.Returns("roomId;;eventName;;");

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("eventroles_roleid_required");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomNotFound_WhenRoomDoesNotExist()
    {
        _context.Target.Returns("unknownroom;;My Event;;123456");
        _roomsManager.GetRoom("unknownroom").Returns((IRoom)null);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("eventroles_room_not_found");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSaveMappingAndReply_WhenAllDataIsValid()
    {
        _context.Target.Returns("testroom;;My Event;;987654");
        _roomsManager.GetRoom("testroom").Returns(_room);

        await _command.RunAsync(_context);

        await _eventRoleMappingService.Received(1).SaveMappingAsync(
            Arg.Is<EventRoleMapping>(m => m.EventName == "My Event" && m.RoomId == "testroom" && m.DiscordRoleId == "987654"),
            Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("eventroles_saved", Arg.Any<object[]>());
    }

    [Test]
    [TestCase("testroom;;My Event;;987654", ExpectedResult = "testroom")]
    public string Test_GetTargetRoomId_ShouldReturnRoomTheCommandActsOn(string target)
    {
        // Arrange
        _context.Target.Returns(target);

        // Act & Assert
        return _command.GetTargetRoomId(_context);
    }
}
