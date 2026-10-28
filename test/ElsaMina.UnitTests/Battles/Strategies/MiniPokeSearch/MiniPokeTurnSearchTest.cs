using System.Text.Json.Nodes;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.MiniPokeSearch;
using ElsaMina.Battles.Strategies.MiniPokeV2;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeSearch;

/// <summary>
/// The search with a scripted simulator: Earthquake hits Eelektross's Levitate, Rock Blast takes it to 40%, and the
/// value is the opponent's missing HP. tools/minipoke/tests/test_search.py checks the Python search the same way.
/// </summary>
public class MiniPokeTurnSearchTest
{
    private const int EARTHQUAKE = 6;
    private const int ROCK_BLAST = 7;
    private const int OPPONENT_ACTIVE_SLOT = 6;
    private const int HP_FEATURE = 5;
    private static readonly MiniPokeModelOptions OPTIONS = new(BattleDecisionStrategy.MiniPokeV2_1M,
        Path.Join(AppContext.BaseDirectory, "Models", "MiniPokeV2_1M"));
    private static readonly string[] END_OF_TURN = ["|upkeep", "|turn|2"];

    private SearchTestBattle _battle;
    private IShowdownSimulator _simulator;
    private IMiniPokeModel _model;
    private MiniPokeV2Encoder _encoder;
    private MiniPokeTurnSearch _search;

    [SetUp]
    public void SetUp()
    {
        _battle = new SearchTestBattle();
        _simulator = Substitute.For<IShowdownSimulator>();
        _simulator.IsAvailable.Returns(true);
        _model = Substitute.For<IMiniPokeModel>();
        _model.Evaluate(Arg.Any<BattleObservation>()).Returns(call =>
        {
            var observation = call.Arg<BattleObservation>();
            var hp = observation.MonFeatures[OPPONENT_ACTIVE_SLOT * observation.MonFeatureSize + HP_FEATURE];
            return (new float[BattleObservation.ActionCount], -hp);
        });
        _encoder = new MiniPokeV2Encoder(OPTIONS, TestBattleDex.Instance);
        _search = new MiniPokeTurnSearch(_simulator, _battle.Parser);
    }

    private static SimulationLeaf Leaf(params string[] lines) => new([..lines, ..END_OF_TURN], null);

    private void SimulatorReplies(SimulationLeaf earthquake, SimulationLeaf rockBlast)
    {
        // The third candidate's turns are refused (null): the search keeps the two others
        var reply = new SimulationReply(
        [
            new SimulationDeterminization(["coil"], [[[earthquake]], [[rockBlast]], [[null]]])
        ], null);
        _simulator.SimulateAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>()).Returns(reply);
    }

    private Task<int?> ChooseAsync()
    {
        var observation = _encoder.Encode(_battle.Context);
        var logits = Enumerable.Repeat(-10f, BattleObservation.ActionCount).ToArray();
        logits[EARTHQUAKE] = 5f;
        logits[ROCK_BLAST] = 4f;
        return _search.ChooseAsync(_battle.Context, logits, observation.Mask, observation, _model, _encoder);
    }

    [Test]
    public async Task Test_ChooseAsync_ShouldPlayTheCandidateWithTheBestSimulatedTurn()
    {
        // Arrange
        SimulatorReplies(
            Leaf("|move|p1a: Sandaconda|Earthquake|p2a: Eelektross",
                "|-immune|p2a: Eelektross|[from] ability: Levitate"),
            Leaf("|move|p1a: Sandaconda|Rock Blast|p2a: Eelektross", "|-damage|p2a: Eelektross|40/100"));

        // Act
        var action = await ChooseAsync();

        // Assert
        Assert.That(action, Is.EqualTo(ROCK_BLAST));
    }

    [Test]
    public async Task Test_ChooseAsync_ShouldAskForTheModelsLikelyActions()
    {
        // Arrange
        JsonObject sent = null;
        var reply = new SimulationReply([new SimulationDeterminization(["coil"], [[[Leaf()]], [[Leaf()]], [[Leaf()]]])],
            null);
        _simulator.SimulateAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            sent = call.Arg<JsonObject>();
            return reply;
        });

        // Act
        await ChooseAsync();

        // Assert: the three most likely actions
        var ours = sent["ours"].AsArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ours, Has.Count.EqualTo(3));
            Assert.That((string)ours[0]["move"], Is.EqualTo("earthquake"));
            Assert.That((string)ours[1]["move"], Is.EqualTo("rockblast"));
            Assert.That((bool)ours[1]["tera"], Is.False);
            Assert.That((string)sent["position"]["us"], Is.EqualTo("p1"));
        }
    }

    [Test]
    public async Task Test_ChooseAsync_ShouldPreferAWin_WhenATurnEndsTheBattle()
    {
        // Arrange
        SimulatorReplies(new SimulationLeaf(["|win|Elsa"], "p1"),
            Leaf("|move|p1a: Sandaconda|Rock Blast|p2a: Eelektross", "|-damage|p2a: Eelektross|0 fnt"));

        // Act
        var action = await ChooseAsync();

        // Assert
        Assert.That(action, Is.EqualTo(EARTHQUAKE));
    }

    [Test]
    public async Task Test_ChooseAsync_ShouldNotSearch_WhenTheSimulatorIsMissing()
    {
        // Arrange
        _simulator.IsAvailable.Returns(false);

        // Act
        var action = await ChooseAsync();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(action, Is.Null);
            await _simulator.DidNotReceive().SimulateAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>());
        }
    }
}
