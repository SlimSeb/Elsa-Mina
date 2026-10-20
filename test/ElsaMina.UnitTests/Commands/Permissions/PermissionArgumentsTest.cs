using ElsaMina.Commands.Permissions;
using ElsaMina.Core.Contexts;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Permissions;

public class PermissionArgumentsTest
{
    [Test]
    [TestCase("alice, %", new[] { "alice", "%" })]
    [TestCase("alice, %, 3d", new[] { "alice", "%", "3d" })]
    [TestCase("", new string[0])]
    public void Test_Parse_ShouldUseRoomOfTheMessage_WhenInARoom(string target, string[] expectedValues)
    {
        // Arrange
        var context = CreateContext(target, isPrivateMessage: false);

        // Act
        var arguments = PermissionArguments.Parse(context);

        // Assert
        Assert.That(arguments.RoomId, Is.EqualTo("contextroom"));
        Assert.That(arguments.Values, Is.EqualTo(expectedValues));
    }

    [Test]
    [TestCase("alice, %, French Room", "frenchroom", new[] { "alice", "%" })]
    [TestCase("alice, %, 3d, franais", "franais", new[] { "alice", "%", "3d" })]
    [TestCase("franais", "franais", new string[0])]
    [TestCase("alice, %, !!!", null, new[] { "alice", "%" })]
    [TestCase("", null, new string[0])]
    public void Test_Parse_ShouldUseLastArgumentAsRoom_WhenInPrivateMessage(string target, string expectedRoomId,
        string[] expectedValues)
    {
        // Arrange
        var context = CreateContext(target, isPrivateMessage: true);

        // Act
        var arguments = PermissionArguments.Parse(context);

        // Assert
        Assert.That(arguments.RoomId, Is.EqualTo(expectedRoomId));
        Assert.That(arguments.Values, Is.EqualTo(expectedValues));
    }

    private static IContext CreateContext(string target, bool isPrivateMessage)
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns(target);
        context.IsPrivateMessage.Returns(isPrivateMessage);
        context.RoomId.Returns("contextroom");
        return context;
    }
}
