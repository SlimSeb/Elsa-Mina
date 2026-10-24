using System.Text.Json;
using System.Text.Json.Nodes;
using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.MiniPokeV2;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeV2;

/// <summary>
/// minipoke-v2's encoder on a battle where the opponent's Tinkaton protects twice. MiniPokeV2ParityTest checks
/// every feature against encoder_v2.py.
/// </summary>
public class MiniPokeV2EncoderTest
{
    private const int CANDIDATE_MOVES = 8;
    private const int PRIOR_FEATURES = 24;
    private const int HAS_SETS = 20;

    private static readonly MiniPokeModelOptions OPTIONS = new(BattleDecisionStrategy.MiniPokeV2_1M,
        Path.Join(AppContext.BaseDirectory, "Models", "MiniPokeV2_1M"));
    private static readonly string[] PROTECT_ONLY = ["protect"];

    private BattleMessageParser _parser;
    private BattleContext _context;
    private string _directory;

    [SetUp]
    public void SetUp()
    {
        _parser = new BattleMessageParser(TestBattleDex.Instance);
        _context = new BattleContext("battle-gen9randombattle-1");
        _directory = Directory.CreateTempSubdirectory("minipoke").FullName;
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_directory, true);
    }

    private void Apply(string line)
    {
        _parser.TryApplyMessage(line.Split("|"), _context.RoomId, _context, out _);
    }

    private void ApplyRequest()
    {
        var pokemon = new[]
        {
            new
            {
                ident = "p1: Garchomp", details = "Garchomp, L80", condition = "100/100", active = true,
                stats = new { atk = 1, def = 1, spa = 1, spd = 1, spe = 1 }, moves = PROTECT_ONLY,
                baseAbility = "roughskin", item = "leftovers", pokeball = "pokeball", ability = "roughskin"
            }
        };
        var request = new
        {
            active = new[] { new { moves = new[] { new { move = "Protect", id = "protect", pp = 16, maxpp = 16, target = "self", disabled = false } } } },
            side = new { name = "Elsa", id = "p1", pokemon },
            rqid = 1
        };
        Apply($"|request|{JsonSerializer.Serialize(request)}");
    }

    private void PlayTwoProtects()
    {
        Apply("|switch|p1a: Garchomp|Garchomp, L80|100/100");
        Apply("|switch|p2a: Tinkaton|Tinkaton, L82, F|100/100");
        Apply("|turn|1");
        ApplyRequest();
        Apply("|move|p2a: Tinkaton|Protect|p2a: Tinkaton");
        Apply("|turn|2");
        Apply("|move|p2a: Tinkaton|Protect|p2a: Tinkaton");
        Apply("|turn|3");
        ApplyRequest();
    }

    /// <summary>
    /// A copy of the model's spec and set priors, changed by <paramref name="tamper"/>.
    /// </summary>
    private MiniPokeModelOptions TamperedModel(Action<JsonNode> tamper)
    {
        var spec = JsonNode.Parse(File.ReadAllText(OPTIONS.EncoderSpecPath));
        tamper(spec);
        File.WriteAllText(Path.Join(_directory, "encoder_spec.json"), spec.ToJsonString());
        File.Copy(Path.Join(OPTIONS.ModelDirectory, "set_priors.json"), Path.Join(_directory, "set_priors.json"));
        return OPTIONS with { ModelDirectory = _directory };
    }

    [Test]
    public void Test_Encode_ShouldWriteHistory_WhenOpponentProtectedTwice()
    {
        // Arrange
        var encoder = new MiniPokeV2Encoder(OPTIONS, TestBattleDex.Instance);
        PlayTwoProtects();

        // Act
        var observation = encoder.Encode(_context);

        // Assert: both actives came in before turn 1, and Protect rules a Choice item out
        var history = observation.ExtraFeatures["hist_feats"];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(observation.ExtraIds["hist_ids"], Is.EqualTo(new long[] { 0, 636 }));
            Assert.That(history[0], Is.EqualTo(0.3f), "opponent active turns");
            Assert.That(history[1], Is.EqualTo(0.3f), "our active turns");
            Assert.That(history[2], Is.Zero, "opponent switched by choice");
            Assert.That(history[4], Is.EqualTo((float)(2 / 3.0)), "opponent protect counter");
            Assert.That(history[5], Is.Zero, "our protect counter");
            Assert.That(history[6], Is.EqualTo(1f), "opponent same move twice");
            Assert.That(history[7], Is.Zero, "opponent choice locked");
        }
    }

    [Test]
    public void Test_Encode_ShouldWriteSetPriors_OnlyForRevealedOpponents()
    {
        // Arrange
        var encoder = new MiniPokeV2Encoder(OPTIONS, TestBattleDex.Instance);
        PlayTwoProtects();

        // Act
        var observation = encoder.Encode(_context);

        // Assert: Tinkaton's candidates leave its revealed Protect out
        var candidates = observation.ExtraIds["cand_ids"];
        var priorFeatures = observation.ExtraFeatures["prior_feats"];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(candidates.Take(CANDIDATE_MOVES).Count(id => id != 0), Is.GreaterThan(0));
            Assert.That(candidates.Take(CANDIDATE_MOVES), Does.Not.Contain(636L));
            Assert.That(candidates.Skip(CANDIDATE_MOVES), Is.All.Zero);
            Assert.That(priorFeatures[HAS_SETS], Is.EqualTo(1f));
            Assert.That(priorFeatures.Skip(PRIOR_FEATURES), Is.All.Zero);
        }
    }

    [Test]
    public void Test_Constructor_ShouldThrow_WhenModelReadsHistoryTokens()
    {
        // Arrange
        var options = TamperedModel(spec => spec["model"]["history_tokens"] = 16);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => _ = new MiniPokeV2Encoder(options, TestBattleDex.Instance));
    }

    [Test]
    public void Test_Constructor_ShouldThrow_WhenSetPriorsAreNotTheExportedOnes()
    {
        // Arrange
        var options = TamperedModel(spec => spec["set_priors"]["sha256"] = new string('0', 64));

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _ = new MiniPokeV2Encoder(options, TestBattleDex.Instance));
        Assert.That(exception.Message, Does.Contain("set_priors.json"));
    }

    [Test]
    public void Test_Constructor_ShouldThrow_WhenSpecInputsAreNotTheOnesItWrites()
    {
        // Arrange: a model trained without the history features
        var options = TamperedModel(spec => spec["model"]["history"] = false);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _ = new MiniPokeV2Encoder(options, TestBattleDex.Instance));
        Assert.That(exception.Message, Does.Contain("hist_feats"));
    }

    [Test]
    public void Test_Constructor_ShouldThrow_WhenSpecIsV1()
    {
        // Arrange
        var options = OPTIONS with { ModelDirectory = Path.Join(AppContext.BaseDirectory, "Models", "MiniPokeV1_1M") };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _ = new MiniPokeV2Encoder(options, TestBattleDex.Instance));
        Assert.That(exception.Message, Does.Contain("encoder_version 1"));
    }
}
