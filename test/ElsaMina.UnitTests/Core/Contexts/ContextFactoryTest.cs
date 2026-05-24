using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.PrivateMessages;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.UserDetails;
using NSubstitute;
using DateTimeOffset = System.DateTimeOffset;

namespace ElsaMina.UnitTests.Core.Contexts;

public class ContextFactoryTest
{
    private ContextFactory _contextFactory;
    private IConfiguration _configuration;
    private IResourcesService _resourcesService;
    private IRoomsManager _roomsManager;
    private IUserDetailsManager _userDetailsManager;
    private IBot _bot;
    private IPmSendersManager _pmSendersManager;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _resourcesService = Substitute.For<IResourcesService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _userDetailsManager = Substitute.For<IUserDetailsManager>();
        _bot = Substitute.For<IBot>();
        _pmSendersManager = Substitute.For<IPmSendersManager>();

        _contextFactory = new ContextFactory(
            _configuration,
            _resourcesService,
            _roomsManager,
            _userDetailsManager,
            _bot,
            _pmSendersManager
        );
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldReturnNull_WhenPartsLengthIsTooSmall()
    {
        // Arrange
        var parts = new[] { "x" };

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldReturnNull_WhenUnknownMessageType()
    {
        // Arrange
        var parts = new[] { "", "weird", "data" };

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldReturnNull_WhenRoomIsNull()
    {
        // Arrange
        string[] parts = ["", "c:", "123", "user1", "!test"];
        _roomsManager.GetRoom("room1").Returns((IRoom)null);

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts, "room1");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldReturnNull_WhenUserNotFoundInRoom()
    {
        // Arrange
        string[] parts = ["", "c:", "123", "missingUser", "!test"];
        var room = Substitute.For<IRoom>();
        room.Users.Returns(new Dictionary<string, IUser>());
        _roomsManager.GetRoom("room1").Returns(room);
        _configuration.Trigger.Returns("!");

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts, "room1");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldReturnRoomContext_WhenValid()
    {
        // Arrange
        string[] parts = ["", "c:", "123456", "USER1", "!hello world"];
        var room = Substitute.For<IRoom>();
        var user = Substitute.For<IUser>();

        room.Users.Returns(new Dictionary<string, IUser> { ["user1"] = user });
        _roomsManager.GetRoom("lobby").Returns(room);
        _configuration.Trigger.Returns("!");

        var expectedDateTime = new DateTimeOffset(1970, 01, 02, 10, 17, 36, TimeSpan.Zero);

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts, "lobby");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.TypeOf<RoomContext>());
            var rc = (RoomContext)result;
            Assert.That(rc.Command, Is.EqualTo("hello"));
            Assert.That(rc.Target, Is.EqualTo("world"));
            Assert.That(rc.Message, Is.EqualTo("!hello world"));
            Assert.That(rc.Timestamp, Is.EqualTo(expectedDateTime));
        }
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldHandleCommandWithoutArgs_Room()
    {
        // Arrange
        string[] parts = ["", "c:", "99", "user", "!ping"];
        var room = Substitute.For<IRoom>();
        var user = Substitute.For<IUser>();

        room.Users.Returns(new Dictionary<string, IUser> { ["user"] = user });
        _roomsManager.GetRoom("main").Returns(room);
        _configuration.Trigger.Returns("!");

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts, "main");

        // Assert
        var rc = (RoomContext)result;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rc.Command, Is.EqualTo("ping"));
            Assert.That(rc.Target, Is.EqualTo(string.Empty));
        }
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldNotReturnNull_WhenMessageOnlyTrigger_Room()
    {
        // Arrange
        string[] parts = ["", "c:", "99", "user", "!"];
        var room = Substitute.For<IRoom>();
        var user = Substitute.For<IUser>();

        room.Users.Returns(new Dictionary<string, IUser> { ["user"] = user });
        _roomsManager.GetRoom("main").Returns(room);
        _configuration.Trigger.Returns("!");

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts, "main");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Command, Is.Null);
            Assert.That(result.Target, Is.Null);
        }
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldReturnPmContext_WhenValidPmMessage()
    {
        // Arrange
        string[] parts = ["", "pm", "user1", "xyz", "!test abc"];
        var pmUser = Substitute.For<IUser>();

        _pmSendersManager.GetUser("user1").Returns(pmUser);
        _configuration.Trigger.Returns("!");

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.TypeOf<PmContext>());
            var pm = (PmContext)result;
            Assert.That(pm.Command, Is.EqualTo("test"));
            Assert.That(pm.Target, Is.EqualTo("abc"));
        }
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldHandlePmMessageWithoutArgs()
    {
        // Arrange
        string[] parts = ["", "pm", "u", "ignored", "!poke"];
        var user = Substitute.For<IUser>();

        _pmSendersManager.GetUser("u").Returns(user);
        _configuration.Trigger.Returns("!");

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts);

        // Assert
        var pm = (PmContext)result;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pm.Command, Is.EqualTo("poke"));
            Assert.That(pm.Target, Is.EqualTo(string.Empty));
        }
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldNotReturnNull_WhenPmMessageHasNoCommand()
    {
        // Arrange
        string[] parts = ["", "pm", "u", "ignored", "hello"];
        var user = Substitute.For<IUser>();

        _pmSendersManager.GetUser("u").Returns(user);
        _configuration.Trigger.Returns("!");

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Command, Is.Null);
            Assert.That(result.Target, Is.Null);
        }
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldNotReturnNull_WhenPmMessageOnlyTrigger()
    {
        // Arrange
        string[] parts = ["", "pm", "u", "ignored", "!"];
        var user = Substitute.For<IUser>();

        _pmSendersManager.GetUser("u").Returns(user);
        _configuration.Trigger.Returns("!");

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Command, Is.Null);
            Assert.That(result.Target, Is.Null);
        }
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldIgnoreTriggerCaseSensitivity()
    {
        // Arrange
        string[] parts = ["", "pm", "user", "x", "#Ping target"];
        var user = Substitute.For<IUser>();

        _pmSendersManager.GetUser("user").Returns(user);
        _configuration.Trigger.Returns("#");

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts);

        // Assert
        var pm = (PmContext)result;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pm.Command, Is.EqualTo("ping"));
            Assert.That(pm.Target, Is.EqualTo("target"));
        }
    }

    [Test]
    public void Test_TryBuildContextFromReceivedMessage_ShouldHandleMultipleSpaces()
    {
        // Arrange
        string[] parts = ["", "pm", "u", "x", "!cmd     arg1   "];
        var user = Substitute.For<IUser>();

        _pmSendersManager.GetUser("u").Returns(user);
        _configuration.Trigger.Returns("!");

        // Act
        var result = _contextFactory.TryBuildContextFromReceivedMessage(parts);

        // Assert
        var pm = (PmContext)result;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(pm.Command, Is.EqualTo("cmd"));
            Assert.That(pm.Target, Is.EqualTo("    arg1   "));
        }
    }
}
