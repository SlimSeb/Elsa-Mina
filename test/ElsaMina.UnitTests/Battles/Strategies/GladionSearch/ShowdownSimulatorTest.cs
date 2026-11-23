using System.Diagnostics;
using System.Text.Json.Nodes;
using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.Gladion;
using ElsaMina.Battles.Strategies.GladionSearch;
using ElsaMina.Battles.Strategies.GladionObservationV2;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Strategies.GladionSearch;

/// <summary>
/// search_sim.js on a real pokemon-showdown checkout (the one tools/gladion trains with, when it is on this machine):
/// the position ElsaMina builds is the one the script reads.
/// </summary>
public class ShowdownSimulatorTest
{
    private static readonly string SHOWDOWN = Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "minipoke", "pokemon-showdown");

    private ShowdownSimulator _simulator;

    [SetUp]
    public void SetUp()
    {
        var configuration = Substitute.For<IBattlesConfiguration>();
        configuration.ShowdownDirectory.Returns(SHOWDOWN);
        configuration.NodePath.Returns("");
        _simulator = new ShowdownSimulator(configuration);
        Assume.That(_simulator.IsAvailable, "needs Node and a pokemon-showdown checkout in ~/Documents/minipoke");
    }

    [TearDown]
    public void TearDown()
    {
        _simulator.Dispose();
    }

    [Test]
    public async Task Test_SimulateAsync_ShouldPlayOurCandidatesFromTheBuiltPosition()
    {
        // Arrange
        var battle = new SearchTestBattle();
        var request = new JsonObject
        {
            ["position"] = SearchPositionBuilder.Build(battle.Context),
            ["ours"] = new JsonArray(
                new JsonObject { ["kind"] = "move", ["move"] = "earthquake", ["tera"] = false },
                new JsonObject { ["kind"] = "switch", ["name"] = "Clodsire" }),
            ["determinizations"] = 2,
            ["samples"] = 1,
            ["seed"] = 7
        };

        // Act
        var reply = await _simulator.SimulateAsync(request);

        // Assert
        var guess = reply.Determinizations[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reply.Determinizations, Has.Count.EqualTo(2));
            Assert.That(guess.Theirs, Is.Not.Empty);
            Assert.That(guess.Leaves[0][0][0].Lines,
                Does.Contain("|-immune|p2a: Eelektross|[from] ability: Levitate"));
            Assert.That(guess.Leaves[1][0][0].Lines, Has.Some.StartsWith("|switch|p1a: Clodsire"));
        }
    }

    [Test]
    public async Task Test_ChooseAsync_ShouldSearchWithTheRealModel()
    {
        // Arrange: GladionV1_22M itself, end to end (position, simulator, replays, ONNX values)
        var options = new GladionModelOptions(BattleDecisionStrategy.GladionV1_22M,
            Path.Join(AppContext.BaseDirectory, "Models", "GladionV1_22M"));
        using var model = new GladionOnnxModel(options);
        var encoder = new GladionEncoderV2(options, TestBattleDex.Instance);
        var battle = new SearchTestBattle();
        var search = new GladionTurnSearch(_simulator, battle.Parser);
        var observation = encoder.Encode(battle.Context);
        var (logits, _) = model.Evaluate(observation);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var action = await search.ChooseAsync(battle.Context, logits, observation.Mask, observation, model, encoder);

        // Assert: a legal action, within the time a ladder turn allows
        using (Assert.EnterMultipleScope())
        {
            Assert.That(action, Is.Not.Null);
            Assert.That(observation.Mask[action.Value], Is.True);
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
        }

        await TestContext.Out.WriteLineAsync($"searched in {stopwatch.ElapsedMilliseconds} ms: action {action}");
    }
}
