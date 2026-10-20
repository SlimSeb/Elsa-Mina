using ElsaMina.Commands.Badges.UserBadgePanel;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Badges.UserBadgePanel;

public class UserBadgePanelCommandTest
{
    private IBotDbContextFactory _dbContextFactory;
    private IContext _context;
    private UserBadgePanelCommand _command;

    [SetUp]
    public void SetUp()
    {
        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("contextroom");
        _command = new UserBadgePanelCommand(_dbContextFactory, Substitute.For<ITemplatesManager>(),
            Substitute.For<IConfiguration>(), Substitute.For<IRoomsManager>());
    }

    [Test]
    [TestCase(false, "alice", ExpectedResult = "contextroom")]
    [TestCase(false, "alice, Other Room", ExpectedResult = "otherroom")]
    [TestCase(true, "alice, otherroom", ExpectedResult = "otherroom")]
    [TestCase(true, "alice", ExpectedResult = null)]
    public string Test_GetTargetRoomId_ShouldReturnSecondArgument_WhichIsRequiredInPrivateMessage(
        bool isPrivateMessage, string target)
    {
        // Arrange
        _context.IsPrivateMessage.Returns(isPrivateMessage);
        _context.Target.Returns(target);

        // Act & Assert
        return _command.GetTargetRoomId(_context);
    }

    [Test]
    public async Task Test_RunAsync_ShouldAskForRoom_WhenItIsMissingInPrivateMessage()
    {
        // Arrange
        _context.IsPrivateMessage.Returns(true);
        _context.Target.Returns("alice");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("badge_pm_missing_room");
        await _dbContextFactory.DidNotReceiveWithAnyArgs().CreateDbContextAsync(default);
    }
}
