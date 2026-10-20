using ElsaMina.Core.Contexts;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Contexts;

public class ContextExtensionsTest
{
    [Test]
    [TestCase("user, badge, French Room", 2, ExpectedResult = "frenchroom")]
    [TestCase("user, badge", 2, ExpectedResult = null)]
    [TestCase("user, badge, ", 2, ExpectedResult = null)]
    [TestCase("user, badge, !!!", 2, ExpectedResult = null)]
    [TestCase("", 0, ExpectedResult = null)]
    public string Test_GetRoomIdArgument_ShouldReturnNormalizedArgument_WhenItIsNotEmpty(string target, int index)
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.Target.Returns(target);

        // Act & Assert
        return context.GetRoomIdArgument(index);
    }

    [Test]
    public void Test_GetRoomIdArgument_ShouldUseSeparator_WhenOneIsGiven()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.Target.Returns("event;;room, with comma");

        // Act
        var roomId = context.GetRoomIdArgument(1, ";;");

        // Assert
        Assert.That(roomId, Is.EqualTo("roomwithcomma"));
    }

    [Test]
    public void Test_GetRoomIdArgument_ShouldReturnNull_WhenTargetIsNull()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.Target.Returns((string)null);

        // Act & Assert
        Assert.That(context.GetRoomIdArgument(0), Is.Null);
    }
}
