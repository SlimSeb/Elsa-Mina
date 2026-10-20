using System.Text.Json;
using System.Text.Json.Nodes;
using ElsaMina.Battles;
using ElsaMina.Battles.Strategies;
using ElsaMina.Battles.Strategies.MiniPoke;
using ElsaMina.Battles.Strategies.MiniPokeV1;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPokeV1;

/// <summary>
/// minipoke v1's encoder, including what it inherits from <see cref="MiniPokeEncoder"/>. MiniPokeV1ParityTest
/// checks its features against encoder.py.
/// </summary>
public class MiniPokeV1EncoderTest
{
    private static readonly MiniPokeModelOptions OPTIONS = new(BattleDecisionStrategy.MiniPokeV1,
        Path.Join(AppContext.BaseDirectory, "Models", "MiniPokeV1"));
    private static readonly string[] PROTECT_ONLY = ["protect"];

    private BattleMessageParser _parser;
    private BattleContext _context;

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
        var encoder = new MiniPokeV1Encoder(OPTIONS, TestBattleDex.Instance);
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
        var encoder = new MiniPokeV1Encoder(OPTIONS, TestBattleDex.Instance);
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
        var encoder = new MiniPokeV1Encoder(OPTIONS, TestBattleDex.Instance);
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
        var encoder = new MiniPokeV1Encoder(OPTIONS with { ModelDirectory = "missing" }, TestBattleDex.Instance);
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
        var directory = Directory.CreateTempSubdirectory("minipoke").FullName;
        var spec = JsonNode.Parse(File.ReadAllText(OPTIONS.EncoderSpecPath));
        spec[size] = value;
        File.WriteAllText(Path.Join(directory, "encoder_spec.json"), spec.ToJsonString());

        try
        {
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
                _ = new MiniPokeV1Encoder(OPTIONS with { ModelDirectory = directory }, TestBattleDex.Instance));
            Assert.That(exception.Message, Does.Contain(size));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
