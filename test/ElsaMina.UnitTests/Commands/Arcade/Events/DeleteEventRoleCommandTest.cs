using ElsaMina.Commands.Arcade.Events;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess.Models;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Arcade.Events;

[TestFixture]
public class DeleteEventRoleCommandTest
{
    private IRoomsManager _roomsManager;
    private IEventRoleMappingService _eventRoleMappingService;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IContext _context;
    private IRoom _room;
    private DeleteEventRoleCommand _command;

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

        _command = new DeleteEventRoleCommand(_roomsManager, _eventRoleMappingService, _templatesManager, _configuration);
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
    public async Task Test_RunAsync_ShouldDoNothing_WhenTargetIsEmpty()
    {
        _context.Target.Returns(string.Empty);

        await _command.RunAsync(_context);

        await _eventRoleMappingService.DidNotReceive()
            .DeleteMappingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomNotFound_WhenRoomDoesNotExist()
    {
        _context.Target.Returns("My Event;;unknownroom");
        _context.RoomId.Returns("testroom");
        _roomsManager.GetRoom("unknownroom").Returns((IRoom)null);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("eventroles_room_not_found");
        await _eventRoleMappingService.DidNotReceive()
            .DeleteMappingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDeleteMappingAndReply_WhenAllDataIsValid()
    {
        _context.Target.Returns("My Event;;testroom");
        _context.RoomId.Returns("testroom");
        _roomsManager.GetRoom("testroom").Returns(_room);

        await _command.RunAsync(_context);

        await _eventRoleMappingService.Received(1).DeleteMappingAsync("My Event", "testroom", Arg.Any<CancellationToken>());
        _context.Received(1).ReplyLocalizedMessage("eventroles_deleted", Arg.Any<object[]>());
    }

    [Test]
    [TestCase("My Event;;testroom", ExpectedResult = "testroom")]
    [TestCase("My Event", ExpectedResult = "defaultroom")]
    public string Test_GetTargetRoomId_ShouldReturnRoomTheCommandActsOn(string target)
    {
        // Arrange
        _context.Target.Returns(target);
        _context.RoomId.Returns("defaultroom");

        // Act & Assert
        return _command.GetTargetRoomId(_context);
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseContextRoomId_WhenNoRoomSpecifiedInTarget()
    {
        _context.Target.Returns("My Event");
        _context.RoomId.Returns("defaultroom");
        _roomsManager.GetRoom("defaultroom").Returns(_room);

        await _command.RunAsync(_context);

        await _eventRoleMappingService.Received(1).DeleteMappingAsync("My Event", "defaultroom", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRefreshDashboard_WhenDeletionSucceeds()
    {
        _context.Target.Returns("My Event;;testroom");
        _context.RoomId.Returns("testroom");
        _roomsManager.GetRoom("testroom").Returns(_room);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyHtmlPage("testroomeventroles", Arg.Any<string>());
    }
}
