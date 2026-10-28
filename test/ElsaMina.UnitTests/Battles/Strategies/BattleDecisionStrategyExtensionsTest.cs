using ElsaMina.Battles.Strategies;

namespace ElsaMina.UnitTests.Battles.Strategies;

[TestFixture]
public class BattleDecisionStrategyExtensionsTest
{
    [TestCase(BattleDecisionStrategy.DamageCalc, "damagecalc")]
    [TestCase(BattleDecisionStrategy.MiniPokeV1_4M, "minipokev1-4m")]
    [TestCase(BattleDecisionStrategy.MiniPokeV2_1M, "minipokev2-1m")]
    [TestCase(BattleDecisionStrategy.MiniPokeV2_1_1M, "minipokev2.1-1m")]
    [TestCase(BattleDecisionStrategy.MiniPokeV2_1M_Search, "minipokev2-1m+search")]
    [TestCase(BattleDecisionStrategy.MiniPokeV2_1_1M_Search, "minipokev2.1-1m+search")]
    public void Test_GetName_ShouldSeparateVersionPartsWithDotsAndSizeWithDash(BattleDecisionStrategy strategy,
        string expectedName)
    {
        // Act
        var name = strategy.GetName();

        // Assert
        Assert.That(name, Is.EqualTo(expectedName));
    }

    [Test]
    public void Test_WithSearch_ShouldGiveTheModelsSearchVariant()
    {
        // Act & Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(BattleDecisionStrategy.MiniPokeV2_4M.WithSearch(),
                Is.EqualTo(BattleDecisionStrategy.MiniPokeV2_4M_Search));
            Assert.That(BattleDecisionStrategy.MiniPokeV2_4M_Search.IsSearch(), Is.True);
            Assert.That(BattleDecisionStrategy.MiniPokeV2_4M.IsSearch(), Is.False);
        }
    }

    [TestCase(BattleDecisionStrategy.DamageCalc)]
    [TestCase(BattleDecisionStrategy.MiniPokeV1_1M_Search)]
    public void Test_WithSearch_ShouldBeNull_WhenTheStrategyHasNoSearchVariant(BattleDecisionStrategy strategy)
    {
        // Act & Assert
        Assert.That(strategy.WithSearch(), Is.Null);
    }

    [Test]
    public void Test_WithSearch_ShouldExistForEveryModel()
    {
        // Arrange
        var models = Enum.GetValues<BattleDecisionStrategy>()
            .Where(strategy => strategy != BattleDecisionStrategy.DamageCalc && !strategy.IsSearch());

        // Act & Assert: BattlesModule registers each model's search variant from this value
        Assert.That(models.Where(model => model.WithSearch() == null), Is.Empty);
    }
}
