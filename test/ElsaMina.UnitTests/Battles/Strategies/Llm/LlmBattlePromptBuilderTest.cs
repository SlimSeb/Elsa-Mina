using ElsaMina.Battles;
using ElsaMina.Battles.Strategies.Llm;
using ElsaMina.Battles.Strategies.Prediction;

namespace ElsaMina.UnitTests.Battles.Strategies.Llm;

[TestFixture]
public class LlmBattlePromptBuilderTest
{
    private LlmBattlePromptBuilder _promptBuilder;

    [SetUp]
    public void SetUp()
    {
        _promptBuilder = new LlmBattlePromptBuilder();
    }

    [Test]
    public void Test_BuildSystemPrompt_ShouldContainSchemaAndTacticalGuidelines()
    {
        // Act
        var systemPrompt = _promptBuilder.BuildSystemPrompt();

        // Assert
        Assert.That(systemPrompt, Does.Contain("You are an elite competitive Pokémon Showdown AI battle engine."));
        Assert.That(systemPrompt, Does.Contain("\"decision\": \"move\" | \"switch\" | \"teampreview\""));
        Assert.That(systemPrompt, Does.Contain("\"index\": <1-based number>"));
        Assert.That(systemPrompt, Does.Contain("\"terastallize\": <true | false>"));
    }

    [Test]
    public void Test_BuildTeamPreviewPrompt_ShouldIncludeFormatOurTeamOpponentTeamAndChoices()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-1001")
        {
            SidePokemon =
            [
                new BattlePokemonState
                {
                    Ident = "p1: Garchomp",
                    Details = "Garchomp, L80, M",
                    MaxHp = 300,
                    CurrentHp = 300,
                    Ability = "Rough Skin",
                    Item = "Leftovers",
                    TeraType = "Steel",
                    Stats = new BattlePokemonStats(240, 190, 160, 170, 210),
                    Moves = ["Earthquake", "Swords Dance", "Scale Shot", "Stealth Rock"]
                },
                new BattlePokemonState
                {
                    Ident = "p1: Iron Valiant",
                    Details = "Iron Valiant, L80",
                    MaxHp = 250,
                    CurrentHp = 250,
                    Ability = "Quark Drive",
                    Item = "Booster Energy",
                    TeraType = "Fairy",
                    Stats = new BattlePokemonStats(240, 180, 230, 140, 230),
                    Moves = ["Moonblast", "Close Combat", "Knock Off", "Encore"]
                }
            ],
            OpponentPokemon =
            [
                new OpponentPokemonState
                {
                    Species = "Great Tusk",
                    Level = 80
                },
                new OpponentPokemonState
                {
                    Species = "Kingambit",
                    Level = 80
                }
            ]
        };

        // Act
        var prompt = _promptBuilder.BuildTeamPreviewPrompt(context, OpponentPrediction.Empty);

        // Assert
        Assert.That(prompt, Does.Contain("Format: gen9ou"));
        Assert.That(prompt, Does.Contain("Slot 1: Garchomp"));
        Assert.That(prompt, Does.Contain("Slot 2: Iron Valiant"));
        Assert.That(prompt, Does.Contain("Great Tusk"));
        Assert.That(prompt, Does.Contain("Kingambit"));
        Assert.That(prompt, Does.Contain("- TEAM 1: Lead with Garchomp"));
        Assert.That(prompt, Does.Contain("- TEAM 2: Lead with Iron Valiant"));
    }

    [Test]
    public void Test_BuildForcedSwitchPrompt_ShouldIncludeHazardsOpponentAndCandidates()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-1002")
        {
            OwnSideStealthRock = true,
            OwnSideSpikesLayers = 1,
            SidePokemon =
            [
                new BattlePokemonState
                {
                    Ident = "p1: Garchomp",
                    Details = "Garchomp, L80, M",
                    IsFainted = true,
                    CurrentHp = 0,
                    MaxHp = 300
                },
                new BattlePokemonState
                {
                    Ident = "p1: Corviknight",
                    Details = "Corviknight, L80, F",
                    CurrentHp = 280,
                    MaxHp = 280,
                    Ability = "Pressure",
                    Item = "Leftovers",
                    Stats = new BattlePokemonStats(170, 220, 120, 180, 140),
                    Moves = ["Brave Bird", "Roost", "Defog", "U-turn"]
                }
            ],
            OpponentPokemon =
            [
                new OpponentPokemonState
                {
                    Species = "Great Tusk",
                    Level = 80,
                    HpPercent = 85.0,
                    IsActive = true,
                    LastUsedMove = "Headlong Rush"
                }
            ]
        };

        var prediction = new OpponentPrediction(
            [new PredictedMove("Headlong Rush", 0.9)],
            new PredictedSpread("Jolly", 0, 252, 4, 0, 0, 252));

        // Act
        var prompt = _promptBuilder.BuildForcedSwitchPrompt(context, prediction, [2]);

        // Assert
        Assert.That(prompt, Does.Contain("FORCED SWITCH"));
        Assert.That(prompt, Does.Contain("Stealth Rock"));
        Assert.That(prompt, Does.Contain("1 layer(s) Spikes"));
        Assert.That(prompt, Does.Contain("Great Tusk"));
        Assert.That(prompt, Does.Contain("Slot 2: Corviknight"));
        Assert.That(prompt, Does.Contain("- SWITCH 2: Switch in Corviknight"));
    }

    [Test]
    public void Test_BuildTurnPrompt_ShouldIncludeActiveMatchupDamageCalcsSpeedAndChoices()
    {
        // Arrange
        var context = new BattleContext("battle-gen9ou-1003")
        {
            SidePokemon =
            [
                new BattlePokemonState
                {
                    Ident = "p1: Garchomp",
                    Details = "Garchomp, L80, M",
                    CurrentHp = 250,
                    MaxHp = 300,
                    IsActive = true,
                    Ability = "Rough Skin",
                    Item = "Leftovers",
                    TeraType = "Steel",
                    Stats = new BattlePokemonStats(240, 190, 160, 170, 210),
                    Moves = ["Earthquake", "Swords Dance", "Scale Shot", "Stealth Rock"]
                },
                new BattlePokemonState
                {
                    Ident = "p1: Rotom-Wash",
                    Details = "Rotom-Wash, L80",
                    CurrentHp = 200,
                    MaxHp = 200,
                    IsActive = false,
                    Ability = "Levitate",
                    Stats = new BattlePokemonStats(130, 210, 210, 210, 180),
                    Moves = ["Hydro Pump", "Volt Switch", "Will-O-Wisp", "Pain Split"]
                }
            ],
            ActiveSlots =
            [
                new BattleActiveSlot
                {
                    CanTerastallize = "Steel",
                    Trapped = false,
                    Moves =
                    [
                        new BattleMoveState { Name = "Earthquake", Id = "earthquake", Pp = 10, MaxPp = 10, Type = "Ground" },
                        new BattleMoveState { Name = "Swords Dance", Id = "swordsdance", Pp = 20, MaxPp = 20, Type = "Normal" },
                        new BattleMoveState { Name = "Scale Shot", Id = "scaleshot", Pp = 20, MaxPp = 20, Type = "Dragon" },
                        new BattleMoveState { Name = "Stealth Rock", Id = "stealthrock", Pp = 20, MaxPp = 20, Type = "Rock" }
                    ]
                }
            ],
            OpponentPokemon =
            [
                new OpponentPokemonState
                {
                    Species = "Heatran",
                    Level = 80,
                    HpPercent = 100.0,
                    IsActive = true
                }
            ]
        };

        var prediction = new OpponentPrediction(
            [new PredictedMove("Magma Storm", 0.9), new PredictedMove("Earth Power", 0.8)],
            new PredictedSpread("Modest", 252, 0, 0, 252, 4, 0));

        // Act
        var prompt = _promptBuilder.BuildTurnPrompt(context, prediction);

        // Assert
        Assert.That(prompt, Does.Contain("BATTLE TURN"));
        Assert.That(prompt, Does.Contain("OUR ACTIVE POKÉMON"));
        Assert.That(prompt, Does.Contain("Garchomp"));
        Assert.That(prompt, Does.Contain("Heatran"));
        Assert.That(prompt, Does.Contain("Earthquake"));
        Assert.That(prompt, Does.Contain("4x Ultra Effective")); // Ground vs Fire/Steel is 4x super effective
        Assert.That(prompt, Does.Contain("- MOVE 1: Use Earthquake"));
        Assert.That(prompt, Does.Contain("- MOVE 1 TERA: Use Earthquake (with Terastallize into Steel)"));
        Assert.That(prompt, Does.Contain("- SWITCH 2: Switch to Rotom-Wash"));
    }

    [Test]
    public void Test_BuildTurnPrompt_ShouldHandleTurn0WithNullActiveOpponent()
    {
        // Arrange (Turn 0 before opponent switches in, as in random battles)
        var context = new BattleContext("battle-gen9randombattle-999")
        {
            SidePokemon =
            [
                new BattlePokemonState
                {
                    Ident = "p2: Gouging Fire",
                    Details = "Gouging Fire, L74",
                    CurrentHp = 277,
                    MaxHp = 277,
                    IsActive = true,
                    Ability = "protosynthesis",
                    Item = "heavydutyboots",
                    TeraType = "Fairy",
                    Stats = new BattlePokemonStats(213, 222, 139, 181, 178),
                    Moves = ["outrage", "heatcrash", "morningsun", "dragondance"]
                },
                new BattlePokemonState
                {
                    Ident = "p2: Ogerpon",
                    Details = "Ogerpon, L80, F",
                    CurrentHp = 259,
                    MaxHp = 259,
                    IsActive = false,
                    Ability = "defiant",
                    Item = "heavydutyboots",
                    TeraType = "Grass",
                    Stats = new BattlePokemonStats(238, 181, 142, 200, 222),
                    Moves = ["ivycudgel", "spikes", "encore", "uturn"]
                }
            ],
            ActiveSlots =
            [
                new BattleActiveSlot
                {
                    CanTerastallize = "Fairy",
                    Moves =
                    [
                        new BattleMoveState { Name = "Outrage", Id = "outrage", Pp = 16, MaxPp = 16 },
                        new BattleMoveState { Name = "Heat Crash", Id = "heatcrash", Pp = 16, MaxPp = 16 },
                        new BattleMoveState { Name = "Morning Sun", Id = "morningsun", Pp = 8, MaxPp = 8 },
                        new BattleMoveState { Name = "Dragon Dance", Id = "dragondance", Pp = 32, MaxPp = 32 }
                    ]
                }
            ],
            OpponentPokemon = [] // No revealed opponents yet at Turn 0
        };

        // Act
        var prompt = _promptBuilder.BuildTurnPrompt(context, OpponentPrediction.Empty);

        // Assert
        Assert.That(prompt, Does.Contain("BATTLE TURN"));
        Assert.That(prompt, Does.Contain("Gouging Fire"));
        Assert.That(prompt, Does.Contain("Opponent active Pokémon not yet revealed"));
        Assert.That(prompt, Does.Contain("- MOVE 1: Use Outrage"));
        Assert.That(prompt, Does.Contain("- SWITCH 2: Switch to Ogerpon"));
    }
}
