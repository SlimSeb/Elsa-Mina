using System.Text.Json;
using System.Text.Json.Nodes;
using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.Gladion;
using ElsaMina.Battles.Strategies.GladionObservationV1;
using ElsaMina.UnitTests.Battles.Strategies.Gladion;

namespace ElsaMina.UnitTests.Battles.Strategies.GladionObservationV1;

/// <summary>
/// The V1 encoder, including what it inherits from <see cref="GladionEncoder"/>, on a V1 spec. The V2 encoder
/// extends it: GladionEncoderV2ParityTest checks these features against encoder.py's.
/// </summary>
public class GladionEncoderV1Test
{
    private static readonly string[] PROTECT_ONLY = ["protect"];

    private GladionModelOptions _options;
    private BattleMessageParser _parser;
    private BattleContext _context;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var directory = Directory.CreateTempSubdirectory("gladion-encoder-v1").FullName;
        GladionEncoderV1Spec.WriteTo(directory);
        _options = new GladionModelOptions(BattleDecisionStrategy.GladionV1_22M, directory);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Directory.Delete(_options.ModelDirectory, recursive: true);
    }

    [SetUp]
    public void SetUp()
    {
        _parser = new BattleMessageParser(TestBattleDex.Instance);
        _context = new BattleContext("battle-gen9randombattle-1");
    }

    private void Apply(string line)
    {
        _parser.TryApplyMessage(line.Split("|"), _context.RoomId, _context, out _);
    }

    private void StartBattle(params (string Species, string Item)[] team)
    {
        Apply($"|switch|p1a: {team[0].Species}|{team[0].Species}, L80|100/100");
        Apply("|switch|p2a: Tinkaton|Tinkaton, L82, F|100/100");
        Apply("|turn|1");
        var pokemon = team.Select((member, index) => new
        {
            ident = $"p1: {member.Species}", details = $"{member.Species}, L80", condition = "100/100",
            active = index == 0, stats = new { atk = 1, def = 1, spa = 1, spd = 1, spe = 1 },
            moves = PROTECT_ONLY, baseAbility = "pressure", item = member.Item, pokeball = "pokeball",
            ability = "pressure"
        });
        var request = new
        {
            active = new[] { new { moves = new[] { new { move = "Protect", id = "protect", pp = 16, maxpp = 16, target = "self", disabled = false } } } },
            side = new { name = "Elsa", id = "p1", pokemon },
            rqid = 1
        };
        Apply($"|request|{JsonSerializer.Serialize(request)}");
    }

    [Test]
    public void Test_Encode_ShouldHashItemsLikeZlibCrc32_WhenItemsAreKnown()
    {
        // Arrange: expected ids are 2 + zlib.crc32(item) % 1024, computed in Python
        var encoder = new GladionEncoderV1(_options, TestBattleDex.Instance);
        StartBattle(("Zapdos", "leftovers"), ("Garchomp", "choicescarf"), ("Corviknight", "heavydutyboots"),
            ("Gholdengo", ""));

        // Act
        var observation = encoder.Encode(_context);

        // Assert: the opponent's item isn't known yet
        using (Assert.EnterMultipleScope())
        {
            Assert.That(observation.MonIds[1], Is.EqualTo(814));
            Assert.That(observation.MonIds[4], Is.EqualTo(219));
            Assert.That(observation.MonIds[7], Is.EqualTo(878));
            Assert.That(observation.MonIds[10], Is.Zero, "no item is padding");
            Assert.That(observation.MonIds[6 * 3 + 1], Is.EqualTo(1), "unknown item");
        }
    }

    [Test]
    public void Test_Encode_ShouldMapTeamSlotsToRequestOrder_WhenRequestIsReordered()
    {
        // Arrange
        var encoder = new GladionEncoderV1(_options, TestBattleDex.Instance);
        StartBattle(("Zapdos", "leftovers"), ("Garchomp", "choicescarf"));
        _context.SidePokemon.Reverse();

        // Act
        var observation = encoder.Encode(_context);

        // Assert
        Assert.That(observation.TeamSlotToSideIndex, Is.EqualTo(new[] { 1, 0, -1, -1, -1, -1 }));
    }

    [Test]
    public void Test_Encode_ShouldReturnNull_WhenBattleIsDoubles()
    {
        // Arrange
        var encoder = new GladionEncoderV1(_options, TestBattleDex.Instance);
        StartBattle(("Zapdos", "leftovers"));
        _context.ActiveSlots.Add(new BattleActiveSlot());

        // Act
        var observation = encoder.Encode(_context);

        // Assert
        Assert.That(observation, Is.Null);
    }

    [Test]
    public void Test_Encode_ShouldReturnNull_WhenSpecIsMissing()
    {
        // Arrange
        var encoder = new GladionEncoderV1(_options with { ModelDirectory = "missing" }, TestBattleDex.Instance);
        StartBattle(("Zapdos", "leftovers"));

        // Act
        var observation = encoder.Encode(_context);

        // Assert
        Assert.That(observation, Is.Null);
    }

    [TestCase("f_mon", 85)]
    [TestCase("f_move", 40)]
    [TestCase("f_field", 80)]
    public void Test_Constructor_ShouldThrow_WhenSpecFeatureSizeIsNotWhatTheEncoderWrites(string size, int value)
    {
        // Arrange: a spec exported from a different encoder.py
        var directory = Directory.CreateTempSubdirectory("gladion").FullName;
        var spec = JsonNode.Parse(File.ReadAllText(_options.EncoderSpecPath));
        spec[size] = value;
        File.WriteAllText(Path.Join(directory, "encoder_spec.json"), spec.ToJsonString());

        try
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                _ = new GladionEncoderV1(_options with { ModelDirectory = directory }, TestBattleDex.Instance));
            Assert.That(exception.Message, Does.Contain(size));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
