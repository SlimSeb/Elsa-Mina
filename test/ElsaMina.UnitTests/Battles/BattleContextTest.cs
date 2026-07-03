using ElsaMina.Battles;

namespace ElsaMina.UnitTests.Battles;

public class BattleContextTest
{
    [Test]
    public void Test_Format_ShouldReturnFormatSegment_WhenRoomIdIsABattleRoom()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-2544193713");

        // Act & Assert
        Assert.That(context.Format, Is.EqualTo("gen9ou"));
    }

    [Test]
    public void Test_Format_ShouldReturnFormatSegment_WhenRoomIdHasPrivateSuffix()
    {
        // Arrange
        var context = new BattleContext("battle-gen9randombattle-123456-abcdefghijklmnop");

        // Act & Assert
        Assert.That(context.Format, Is.EqualTo("gen9randombattle"));
    }

    [Test]
    public void Test_Format_ShouldReturnEmpty_WhenRoomIdHasNoSegments()
    {
        // Arrange
        var context = new BattleContext("lobby");

        // Act & Assert
        Assert.That(context.Format, Is.EqualTo(""));
    }
}
