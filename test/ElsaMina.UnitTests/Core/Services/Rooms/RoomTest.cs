using System.Globalization;
using ElsaMina.Core.Services.Games;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Rooms;

public class RoomTest
{
    private IRoomParameterStore _roomParameterStore;
    private Room _room;

    [SetUp]
    public void SetUp()
    {
        _roomParameterStore = Substitute.For<IRoomParameterStore>();
        _room = new Room("Français", "franais", new CultureInfo("fr-FR"), TimeZoneInfo.Utc, _roomParameterStore);
    }

    [Test]
    public void Test_Constructor_ShouldDeriveRoomId_WhenRoomIdIsNull()
    {
        // Act
        var room = new Room("My Room", null, CultureInfo.InvariantCulture, TimeZoneInfo.Utc, _roomParameterStore);

        // Assert
        Assert.That(room.RoomId, Is.EqualTo("myroom"));
    }

    [Test]
    public void Test_UpdateMessageQueue_ShouldReturnMessagesMostRecentFirst()
    {
        // Act
        _room.UpdateMessageQueue("Alice", "first");
        _room.UpdateMessageQueue("Bob", "second");
        _room.UpdateMessageQueue("Carl", "third");

        // Assert
        var messages = _room.LastMessages.ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(messages, Has.Count.EqualTo(3));
            Assert.That(messages[0], Is.EqualTo(Tuple.Create("Carl", "third")));
            Assert.That(messages[2], Is.EqualTo(Tuple.Create("Alice", "first")));
        }
    }

    [Test]
    public void Test_UpdateMessageQueue_ShouldCapAtTwelveMessages()
    {
        // Act
        for (var i = 0; i < 20; i++)
        {
            _room.UpdateMessageQueue("User", $"message {i}");
        }

        // Assert
        Assert.That(_room.LastMessages.Count(), Is.EqualTo(12));
    }

    [Test]
    public void Test_InitializeMessageQueueFromLogs_ShouldKeepChatLinesAndSkipRaw()
    {
        // Arrange
        var logs = new[]
        {
            "|c:|1|+Alice|hello",
            "|j|+Bob",
            "|c:|2|+Bob|/raw <div>ignored</div>",
            "|c:|3|+Carl|world"
        };

        // Act
        _room.InitializeMessageQueueFromLogs(logs);

        // Assert
        var messages = _room.LastMessages.ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(messages, Has.Count.EqualTo(2));
            // Most recent first.
            Assert.That(messages[0], Is.EqualTo(Tuple.Create("+Carl", "world")));
            Assert.That(messages[1], Is.EqualTo(Tuple.Create("+Alice", "hello")));
        }
    }

    [Test]
    public void Test_AddUser_ShouldRegisterUserById()
    {
        // Act
        _room.AddUser("+Alice");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_room.Users.ContainsKey("alice"), Is.True);
            Assert.That(_room.Users["alice"].Name, Is.EqualTo("Alice"));
        }
    }

    [Test]
    public void Test_AddUsers_ShouldRegisterEveryUser()
    {
        // Act
        _room.AddUsers(["+Alice", "@Bob", " Carl"]);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_room.Users.ContainsKey("alice"), Is.True);
            Assert.That(_room.Users.ContainsKey("bob"), Is.True);
            Assert.That(_room.Users.ContainsKey("carl"), Is.True);
        }
    }

    [Test]
    public void Test_RemoveUser_ShouldDropUserAndRecordPlayTime()
    {
        // Arrange
        _room.AddUser("+Alice");

        // Act
        _room.RemoveUser("+Alice");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_room.Users.ContainsKey("alice"), Is.False);
            Assert.That(_room.PendingPlayTimeUpdates.ContainsKey("alice"), Is.True);
        }
    }

    [Test]
    public void Test_RemoveUser_ShouldDoNothing_WhenUserWasNeverAdded()
    {
        // Act
        _room.RemoveUser("+Ghost");

        // Assert
        Assert.That(_room.PendingPlayTimeUpdates, Is.Empty);
    }

    [Test]
    public void Test_RenameUser_ShouldReplaceOldUserWithNew()
    {
        // Arrange
        _room.AddUser("+Alice");

        // Act
        _room.RenameUser("+Alice", "@Alicia");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_room.Users.ContainsKey("alice"), Is.False);
            Assert.That(_room.Users.ContainsKey("alicia"), Is.True);
        }
    }

    [Test]
    public void Test_Game_ShouldBeClearedAutomatically_WhenGameEndedEventFires()
    {
        // Arrange
        var game = Substitute.For<IGame>();
        _room.Game = game;

        // Act
        game.GameEnded += Raise.Event<Action>();

        // Assert
        Assert.That(_room.Game, Is.Null);
    }

    [Test]
    public void Test_Game_ShouldUnsubscribeOldGame_WhenReplaced()
    {
        // Arrange
        var oldGame = Substitute.For<IGame>();
        var newGame = Substitute.For<IGame>();
        _room.Game = oldGame;
        _room.Game = newGame;

        // Act - firing the replaced game's end event must not clear the current game.
        oldGame.GameEnded += Raise.Event<Action>();

        // Assert
        Assert.That(_room.Game, Is.SameAs(newGame));
    }

    [Test]
    public async Task Test_GetParameterValueAsync_ShouldDelegateToStore()
    {
        // Arrange
        _roomParameterStore.GetValueAsync(Parameter.Locale, Arg.Any<CancellationToken>())
            .Returns("en-US");

        // Act
        var result = await _room.GetParameterValueAsync(Parameter.Locale);

        // Assert
        Assert.That(result, Is.EqualTo("en-US"));
    }

    [Test]
    public async Task Test_SetParameterValueAsync_ShouldDelegateToStore()
    {
        // Arrange
        _roomParameterStore.SetValueAsync(Parameter.Locale, "en-US", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _room.SetParameterValueAsync(Parameter.Locale, "en-US");

        // Assert
        Assert.That(result, Is.True);
    }
}
