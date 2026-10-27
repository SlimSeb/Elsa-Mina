using System.Text.Json;
using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.MiniPokeV2;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeV2;

/// <summary>
/// The move effect features (encoder_v2.py --move-effects) of a model trained with them, on a battle where our
/// Sandaconda's Earthquake hits Eelektross's Levitate. MiniPokeV2ParityTest checks every value against Python.
/// </summary>
public class MiniPokeV2MoveEffectsTest
{
    private const int EFFECT_FEATURES = 6;
    private static readonly MiniPokeModelOptions OPTIONS = new(BattleDecisionStrategy.MiniPokeV3_1M,
        Path.Join(AppContext.BaseDirectory, "Models", "MiniPokeV3_1M"));
    private static readonly string[] MOVES = ["earthquake", "rockblast", "glare", "coil"];
    private static readonly float[] LEVITATE_REVEALED = [1f, 1f, 0f, 1f, 0f, 0f];
    private static readonly float[] LEVITATE_KNOWN = [1f, 1f, 0f, 0f, 0f, 0f];
    private static readonly string[] GROUND = ["ground"];

    private BattleMessageParser _parser;
    private BattleContext _context;
    private MiniPokeV2Encoder _encoder;

    [SetUp]
    public void SetUp()
    {
        _parser = new BattleMessageParser(TestBattleDex.Instance);
        _context = new BattleContext("battle-gen9randombattle-1");
        _encoder = new MiniPokeV2Encoder(OPTIONS, TestBattleDex.Instance);
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
                ident = "p1: Sandaconda", details = "Sandaconda, L84", condition = "200/258", active = true,
                stats = new { atk = 1, def = 1, spa = 1, spd = 1, spe = 1 }, moves = MOVES,
                baseAbility = "shedskin", item = "leftovers", pokeball = "pokeball", ability = "shedskin"
            }
        };
        var request = new
        {
            active = new[]
            {
                new
                {
                    moves = MOVES.Select(move => new { move, id = move, pp = 16, maxpp = 16, target = "normal",
                        disabled = false })
                }
            },
            side = new { name = "Elsa", id = "p1", pokemon },
            rqid = 1
        };
        Apply($"|request|{JsonSerializer.Serialize(request)}");
    }

    private void Start()
    {
        Apply("|switch|p1a: Sandaconda|Sandaconda, L84|200/258");
        Apply("|switch|p2a: Eelektross|Eelektross, L87|100/100");
        Apply("|turn|1");
        ApplyRequest();
    }

    private float[] EarthquakeFeatures()
    {
        var observation = _encoder.Encode(_context);
        return observation.ExtraFeatures["effect_feats"].Take(EFFECT_FEATURES).ToArray();
    }

    [Test]
    public void Test_Encode_ShouldSeeLevitate_BeforeItIsRevealed()
    {
        // Arrange: Eelektross can only have Levitate
        Start();

        // Act
        var features = EarthquakeFeatures();

        // Assert
        Assert.That(features, Is.EqualTo(LEVITATE_KNOWN));
    }

    [Test]
    public void Test_Encode_ShouldRememberTheImmunity_WhenEarthquakeHadNoEffect()
    {
        // Arrange
        Start();
        Apply("|move|p1a: Sandaconda|Earthquake|p2a: Eelektross");
        Apply("|-immune|p2a: Eelektross|[from] ability: Levitate");
        Apply("|turn|2");
        ApplyRequest();

        // Act
        var features = EarthquakeFeatures();

        // Assert
        Assert.That(features, Is.EqualTo(LEVITATE_REVEALED));
    }

    [Test]
    public void Test_Constructor_ShouldReadTheMoveEffectRules_FromTheSpec()
    {
        // Act
        var spec = EncoderSpec.Load(OPTIONS.EncoderSpecPath);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(spec.Model.MoveEffects, Is.True);
            Assert.That(spec.MoveEffects.AbilityRules["levitate"].Types, Is.EqualTo(GROUND));
            Assert.That(spec.MoveEffects.Features, Has.Count.EqualTo(EFFECT_FEATURES));
        }
    }
}
