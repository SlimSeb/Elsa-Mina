using ElsaMina.Battles;
using ElsaMina.Battles.Strategies.Llm;

namespace ElsaMina.UnitTests.Battles.Strategies.Llm;

[TestFixture]
public class LlmDecisionParsedResultTest
{
    [Test]
    public void Test_Invalid_ShouldCreateInvalidResult()
    {
        // Act
        var result = LlmDecisionParsedResult.Invalid("Could not parse action");

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.DecisionType, Is.Null);
        Assert.That(result.Reasoning, Is.EqualTo("Could not parse action"));
    }

    [Test]
    public void Test_Valid_ShouldCreateValidResult()
    {
        // Act
        var result = LlmDecisionParsedResult.Valid(BattleDecisionType.Move, 2, terastallize: true, reasoning: "Good play");

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DecisionType, Is.EqualTo(BattleDecisionType.Move));
        Assert.That(result.ChoiceIndex, Is.EqualTo(2));
        Assert.That(result.UseTerastallize, Is.True);
        Assert.That(result.Reasoning, Is.EqualTo("Good play"));
    }
}
