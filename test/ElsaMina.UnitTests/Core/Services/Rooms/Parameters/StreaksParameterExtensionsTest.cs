using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Rooms.Parameters;

public class StreaksParameterExtensionsTest
{
    [Test]
    public async Task Test_IsStreaksEnabledAsync_Context_ShouldReturnTrue_WhenRoomIsNull()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.Room.Returns((IRoom)null);

        // Act
        var result = await context.IsStreaksEnabledAsync();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task Test_IsStreaksEnabledAsync_Context_ShouldReturnTrue_WhenParameterIsTrue()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        var room = Substitute.For<IRoom>();
        context.Room.Returns(room);
        room.GetParameterValueAsync(Parameter.StreaksEnabled, Arg.Any<CancellationToken>())
            .Returns(true.ToString());

        // Act
        var result = await context.IsStreaksEnabledAsync();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task Test_IsStreaksEnabledAsync_Context_ShouldReturnFalse_WhenParameterIsFalse()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        var room = Substitute.For<IRoom>();
        context.Room.Returns(room);
        room.GetParameterValueAsync(Parameter.StreaksEnabled, Arg.Any<CancellationToken>())
            .Returns(false.ToString());

        // Act
        var result = await context.IsStreaksEnabledAsync();

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task Test_IsStreaksEnabledAsync_Room_ShouldReturnTrue_WhenRoomIsNull()
    {
        // Arrange
        IRoom room = null;

        // Act
        var result = await room.IsStreaksEnabledAsync();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task Test_IsStreaksEnabledAsync_Room_ShouldReturnTrue_WhenParameterIsTrue()
    {
        // Arrange
        var room = Substitute.For<IRoom>();
        room.GetParameterValueAsync(Parameter.StreaksEnabled, Arg.Any<CancellationToken>())
            .Returns(true.ToString());

        // Act
        var result = await room.IsStreaksEnabledAsync();

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task Test_IsStreaksEnabledAsync_Room_ShouldReturnFalse_WhenParameterIsFalse()
    {
        // Arrange
        var room = Substitute.For<IRoom>();
        room.GetParameterValueAsync(Parameter.StreaksEnabled, Arg.Any<CancellationToken>())
            .Returns(false.ToString());

        // Act
        var result = await room.IsStreaksEnabledAsync();

        // Assert
        Assert.That(result, Is.False);
    }
}
