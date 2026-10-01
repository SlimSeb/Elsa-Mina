using ElsaMina.Core.Services.Dispatch;

namespace ElsaMina.UnitTests.Core.Services.Dispatch;

public class ShowdownFrameTest
{
    [Test]
    [TestCase(">franais\n|c:|1|%Earth|test", "franais")]
    [TestCase(">battle-gen9ou-1\r\n|turn|1", "battle-gen9ou-1")]
    [TestCase(">franais", "franais")]
    [TestCase("|pm| Someone| Bot|hi", "lobby")]
    [TestCase("|c:|1|%Earth|test", "lobby")]
    [TestCase(">\n|c:|1|%Earth|test", "lobby")]
    [TestCase("", "lobby")]
    [TestCase(null, "lobby")]
    public void Test_GetRoomId_ShouldReturnExpectedRoom(string frame, string expectedRoomId)
    {
        // Act
        var roomId = ShowdownFrame.GetRoomId(frame);

        // Assert
        Assert.That(roomId, Is.EqualTo(expectedRoomId));
    }
}
