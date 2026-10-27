using ElsaMina.Battles.Strategies;

namespace ElsaMina.UnitTests.Battles.Strategies;

[TestFixture]
public class BattleDecisionStrategyExtensionsTest
{
    [TestCase(BattleDecisionStrategy.DamageCalc, "damagecalc")]
    [TestCase(BattleDecisionStrategy.MiniPokeV1_4M, "minipokev1-4m")]
    [TestCase(BattleDecisionStrategy.MiniPokeV2_1M, "minipokev2-1m")]
    [TestCase(BattleDecisionStrategy.MiniPokeV2_1_1M, "minipokev2.1-1m")]
    public void Test_GetName_ShouldSeparateVersionPartsWithDotsAndSizeWithDash(BattleDecisionStrategy strategy,
        string expectedName)
    {
        // Act
        var name = strategy.GetName();

        // Assert
        Assert.That(name, Is.EqualTo(expectedName));
    }
}
