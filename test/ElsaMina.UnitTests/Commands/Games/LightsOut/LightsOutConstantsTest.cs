using ElsaMina.Commands.Games.LightsOut;

namespace ElsaMina.UnitTests.Commands.Games.LightsOut;

[TestFixture]
public class LightsOutConstantsTest
{
    [Test]
    public void Test_GetLevelConfig_ShouldReturnLevel1Config_WhenLevelIs1()
    {
        var (gridSize, presses) = LightsOutConstants.GetLevelConfig(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gridSize, Is.EqualTo(5));
            Assert.That(presses, Is.EqualTo(3));
        }
    }

    [Test]
    public void Test_GetLevelConfig_ShouldReturnMaxLevelConfig_WhenLevelExceedsMax()
    {
        var (gridSize, presses) = LightsOutConstants.GetLevelConfig(100);

        var (expectedGridSize, expectedPresses) = LightsOutConstants.LEVEL_CONFIGURATIONS[^1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(gridSize, Is.EqualTo(expectedGridSize));
            Assert.That(presses, Is.EqualTo(expectedPresses));
        }
    }

    [Test]
    public void Test_GetLevelConfig_ShouldReturnLevel1Config_WhenLevelIsBelowMinimum()
    {
        var (gridSize, presses) = LightsOutConstants.GetLevelConfig(0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gridSize, Is.EqualTo(5));
            Assert.That(presses, Is.EqualTo(3));
        }
    }

    [Test]
    public void Test_GetLevelConfig_ShouldReturnLevel15Config_WhenLevelIs15()
    {
        var (gridSize, presses) = LightsOutConstants.GetLevelConfig(15);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gridSize, Is.EqualTo(8));
            Assert.That(presses, Is.EqualTo(22));
        }
    }

    [Test]
    public void Test_MaxLevel_ShouldBe15()
    {
        Assert.That(LightsOutConstants.MAX_LEVEL, Is.EqualTo(15));
    }

    [Test]
    public void Test_LevelConfigurations_ShouldHave15Entries()
    {
        Assert.That(LightsOutConstants.LEVEL_CONFIGURATIONS, Has.Length.EqualTo(15));
    }
}
