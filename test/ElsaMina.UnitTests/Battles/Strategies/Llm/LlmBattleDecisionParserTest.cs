using ElsaMina.Battles;
using ElsaMina.Battles.Strategies.Llm;

namespace ElsaMina.UnitTests.Battles.Strategies.Llm;

[TestFixture]
public class LlmBattleDecisionParserTest
{
    private LlmBattleDecisionParser _parser;

    [SetUp]
    public void SetUp()
    {
        _parser = new LlmBattleDecisionParser();
    }

    [Test]
    public void Test_Parse_ShouldReturnValidMove_WhenJsonIsValid()
    {
        // Arrange
        const string json = """
                            {
                              "reasoning": "Earthquake secures the OHKO on heatran.",
                              "decision": "move",
                              "index": 1,
                              "terastallize": false
                            }
                            """;

        // Act
        var result = _parser.Parse(json);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DecisionType, Is.EqualTo(BattleDecisionType.Move));
        Assert.That(result.ChoiceIndex, Is.EqualTo(1));
        Assert.That(result.UseTerastallize, Is.False);
        Assert.That(result.Reasoning, Is.EqualTo("Earthquake secures the OHKO on heatran."));
    }

    [Test]
    public void Test_Parse_ShouldReturnValidMoveWithTera_WhenJsonHasTerastallizeTrue()
    {
        // Arrange
        const string json = """
                            {
                              "reasoning": "Terastallizing into Water powers up Surging Strikes to break through defensive Great Tusk.",
                              "decision": "move",
                              "index": 2,
                              "terastallize": true
                            }
                            """;

        // Act
        var result = _parser.Parse(json);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DecisionType, Is.EqualTo(BattleDecisionType.Move));
        Assert.That(result.ChoiceIndex, Is.EqualTo(2));
        Assert.That(result.UseTerastallize, Is.True);
    }

    [Test]
    public void Test_Parse_ShouldReturnValidSwitch_WhenJsonHasSwitchDecision()
    {
        // Arrange
        const string json = """
                            {
                              "reasoning": "Switching to Corviknight walling the opponent's physical attacker.",
                              "decision": "switch",
                              "index": 3,
                              "terastallize": false
                            }
                            """;

        // Act
        var result = _parser.Parse(json);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DecisionType, Is.EqualTo(BattleDecisionType.Switch));
        Assert.That(result.ChoiceIndex, Is.EqualTo(3));
        Assert.That(result.UseTerastallize, Is.False);
    }

    [Test]
    public void Test_Parse_ShouldReturnValidTeamPreview_WhenJsonHasTeamPreviewDecision()
    {
        // Arrange
        const string json = """
                            {
                              "reasoning": "Leading with Ting-Lu sets up early hazards against their team composition.",
                              "decision": "teampreview",
                              "index": 4,
                              "terastallize": false
                            }
                            """;

        // Act
        var result = _parser.Parse(json);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DecisionType, Is.EqualTo(BattleDecisionType.TeamPreview));
        Assert.That(result.ChoiceIndex, Is.EqualTo(4));
    }

    [Test]
    public void Test_Parse_ShouldExtractJson_WhenSurroundedByMarkdownOrProse()
    {
        // Arrange
        const string response = """
                                Here is my tactical analysis:
                                The opponent is in kill range from Draco Meteor.

                                ```json
                                {
                                  "reasoning": "Draco Meteor easily KOs.",
                                  "decision": "move",
                                  "index": 1,
                                  "terastallize": false
                                }
                                ```
                                Good luck!
                                """;

        // Act
        var result = _parser.Parse(response);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DecisionType, Is.EqualTo(BattleDecisionType.Move));
        Assert.That(result.ChoiceIndex, Is.EqualTo(1));
    }

    [Test]
    public void Test_Parse_ShouldFallbackToRegex_WhenPlainMoveText()
    {
        // Arrange
        const string text = "I recommend to use MOVE 2 against this target.";

        // Act
        var result = _parser.Parse(text);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DecisionType, Is.EqualTo(BattleDecisionType.Move));
        Assert.That(result.ChoiceIndex, Is.EqualTo(2));
        Assert.That(result.UseTerastallize, Is.False);
    }

    [Test]
    public void Test_Parse_ShouldFallbackToRegex_WhenPlainMoveTeraText()
    {
        // Arrange
        const string text = "We should execute MOVE 3 TERA immediately.";

        // Act
        var result = _parser.Parse(text);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DecisionType, Is.EqualTo(BattleDecisionType.Move));
        Assert.That(result.ChoiceIndex, Is.EqualTo(3));
        Assert.That(result.UseTerastallize, Is.True);
    }

    [Test]
    public void Test_Parse_ShouldFallbackToRegex_WhenPlainSwitchText()
    {
        // Arrange
        const string text = "The best play is to SWITCH 5.";

        // Act
        var result = _parser.Parse(text);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DecisionType, Is.EqualTo(BattleDecisionType.Switch));
        Assert.That(result.ChoiceIndex, Is.EqualTo(5));
    }

    [Test]
    public void Test_Parse_ShouldFallbackToRegex_WhenPlainTeamText()
    {
        // Arrange
        const string text = "Start the battle with TEAM 2.";

        // Act
        var result = _parser.Parse(text);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DecisionType, Is.EqualTo(BattleDecisionType.TeamPreview));
        Assert.That(result.ChoiceIndex, Is.EqualTo(2));
    }

    [Test]
    public void Test_Parse_ShouldReturnInvalid_WhenResponseIsGarbageOrEmpty()
    {
        // Act & Assert
        Assert.That(_parser.Parse(null).IsValid, Is.False);
        Assert.That(_parser.Parse("").IsValid, Is.False);
        Assert.That(_parser.Parse("   ").IsValid, Is.False);
        Assert.That(_parser.Parse("I don't know what to do here.").IsValid, Is.False);
    }
}
