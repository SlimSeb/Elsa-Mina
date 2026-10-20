using System.Text.Json;
using ElsaMina.Battles;
using ElsaMina.Battles.Tracking;

namespace ElsaMina.UnitTests.Battles;

/// <summary>
/// The poke-env-like state <see cref="BattleMessageParser"/> tracks for the minipoke strategies
/// (<see cref="BattleContext.Tracked"/>). We play p1: Garchomp leads, with Pelipper and Gholdengo in the back.
/// </summary>
public class BattleMessageParserTrackingTest
{
    private const string GARCHOMP = "p1: Garchomp";
    private const string PELIPPER = "p1: Pelipper";
    private const string GHOLDENGO = "p1: Gholdengo";

    private static readonly Dictionary<string, Dictionary<string, object>> OUR_TEAM = new()
    {
        [GARCHOMP] = SidePokemon(GARCHOMP, "Garchomp, L80, M", "280/280", "roughskin", "leftovers",
            "earthquake", "outrage", "stealthrock", "swordsdance"),
        [PELIPPER] = SidePokemon(PELIPPER, "Pelipper, L86, F", "250/250", "drizzle", "damprock",
            "hurricane", "surf", "uturn", "roost"),
        [GHOLDENGO] = SidePokemon(GHOLDENGO, "Gholdengo, L77", "260/260", "goodasgold", "choicescarf",
            "makeitrain", "shadowball", "nastyplot", "recover")
    };

    private BattleMessageParser _parser;
    private BattleContext _context;

    private TrackedBattleState Tracked => _context.Tracked;
    private TrackedPokemon Garchomp => TrackedBattleState.Find(Tracked.Team, GARCHOMP);
    private TrackedPokemon Opponent(string name) => TrackedBattleState.Find(Tracked.OpponentTeam, $"p2: {name}");

    [SetUp]
    public void SetUp()
    {
        _parser = new BattleMessageParser(TestBattleDex.Instance);
        _context = new BattleContext("battle-gen9randombattle-1");
    }

    private static Dictionary<string, object> SidePokemon(string ident, string details, string condition,
        string ability, string item, params string[] moves)
    {
        return new Dictionary<string, object>
        {
            ["ident"] = ident, ["details"] = details, ["condition"] = condition, ["active"] = false,
            ["stats"] = new { atk = 100, def = 100, spa = 100, spd = 100, spe = 100 }, ["moves"] = moves,
            ["baseAbility"] = ability, ["item"] = item, ["pokeball"] = "pokeball", ["ability"] = ability,
            ["commanding"] = false, ["reviving"] = false, ["teraType"] = "Fire", ["terastallized"] = ""
        };
    }

    private void Apply(string line)
    {
        _parser.TryApplyMessage(line.Split("|"), _context.RoomId, _context, out _);
    }

    /// <summary>
    /// A request with our team in the given order, the first one active.
    /// </summary>
    private void ApplyRequest(string[] order = null, bool trapped = false, bool forceSwitch = false,
        string canTerastallize = "Fire", Dictionary<string, string> conditions = null)
    {
        order ??= [GARCHOMP, PELIPPER, GHOLDENGO];
        var side = order.Select((ident, index) =>
        {
            var pokemon = new Dictionary<string, object>(OUR_TEAM[ident]) { ["active"] = index == 0 };
            if (conditions != null && conditions.TryGetValue(ident, out var condition))
            {
                pokemon["condition"] = condition;
            }

            return pokemon;
        }).ToList();
        var moves = ((string[])OUR_TEAM[order[0]]["moves"])
            .Select(id => new { move = id, id, pp = 16, maxpp = 16, target = "normal", disabled = false });
        var request = new Dictionary<string, object> { ["side"] = new { name = "Elsa", id = "p1", pokemon = side }, ["rqid"] = 1 };
        if (forceSwitch)
        {
            request["forceSwitch"] = new[] { true };
        }
        else
        {
            request["active"] = new[] { new { moves, canTerastallize, trapped } };
        }

        Apply($"|request|{JsonSerializer.Serialize(request)}");
    }

    private void StartBattle(string opponentSwitch = "|switch|p2a: Tinkaton|Tinkaton, L82, F|100/100")
    {
        Apply("|switch|p1a: Garchomp|Garchomp, L80, M|280/280");
        Apply(opponentSwitch);
        Apply("|turn|1");
        ApplyRequest();
    }

    [Test]
    public void Test_TryApplyMessage_ShouldReplayFirstTurnLines_WhenFirstRequestArrives()
    {
        // Arrange: the first turn's lines come before the request that tells us our side
        Apply("|switch|p1a: Garchomp|Garchomp, L80, M|280/280");
        Apply("|switch|p2a: Tinkaton|Tinkaton, L82, F|100/100");
        Apply("|turn|1");
        Assert.That(Tracked.Team, Is.Empty);

        // Act
        ApplyRequest();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Tracked.PlayerRole, Is.EqualTo("p1"));
            Assert.That(Tracked.Turn, Is.EqualTo(1));
            Assert.That(Tracked.OpponentActivePokemon.Species, Is.EqualTo("tinkaton"));
            Assert.That(Garchomp.FirstTurn, Is.True);
            Assert.That(Tracked.PendingLines, Is.Empty);
            Assert.That(Tracked.IsRequestTracked, Is.True);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldKeepFirstSeenTeamOrder_WhenRequestReordersTeam()
    {
        // Arrange
        StartBattle();
        Apply("|switch|p1a: Gholdengo|Gholdengo, L77|260/260");

        // Act: Showdown puts the active Pokémon first
        ApplyRequest([GHOLDENGO, GARCHOMP, PELIPPER]);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Tracked.Team.Select(pokemon => pokemon.Identifier), Is.EqualTo(new[] { GARCHOMP, PELIPPER, GHOLDENGO }));
            Assert.That(Tracked.ActivePokemon.Identifier, Is.EqualTo(GHOLDENGO));
            Assert.That(Tracked.AvailableSwitches.Select(pokemon => pokemon.Identifier),
                Is.EquivalentTo(new[] { GARCHOMP, PELIPPER }));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldTrackOpponentByIdent_WhenFormeDiffersFromNickname()
    {
        // Arrange
        StartBattle("|switch|p2a: Ogerpon|Ogerpon-Wellspring, L76, F|100/100");

        // Act
        Apply("|-damage|p2a: Ogerpon|48/100");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Tracked.OpponentTeam, Has.Count.EqualTo(1));
            Assert.That(Opponent("Ogerpon").Species, Is.EqualTo("ogerponwellspring"));
            Assert.That(Opponent("Ogerpon").CurrentHpFraction, Is.EqualTo(0.48));
        }
    }

    [TestCase("RainDance", "RAINDANCE")]
    [TestCase("SunnyDay", "SUNNYDAY")]
    [TestCase("Snow", "SNOWSCAPE")]
    [TestCase("Sandstorm|[from] ability: Sand Stream|[of] p2a: Tyranitar", "SANDSTORM")]
    public void Test_TryApplyMessage_ShouldTrackWeather_WhenWeatherStarts(string weather, string expected)
    {
        // Arrange
        StartBattle();

        // Act
        Apply($"|-weather|{weather}");

        // Assert
        Assert.That(Tracked.Weather.Keys, Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void Test_TryApplyMessage_ShouldClearWeather_WhenWeatherEnds()
    {
        // Arrange
        StartBattle();
        Apply("|-weather|RainDance");

        // Act
        Apply("|-weather|none");

        // Assert
        Assert.That(Tracked.Weather, Is.Empty);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldReplaceTerrain_WhenAnotherTerrainStarts()
    {
        // Arrange
        StartBattle();
        Apply("|-fieldstart|move: Trick Room|[of] p2a: Tinkaton");
        Apply("|-fieldstart|move: Electric Terrain|[from] ability: Electric Surge|[of] p2a: Tinkaton");

        // Act
        Apply("|-fieldstart|move: Psychic Terrain");

        // Assert
        Assert.That(Tracked.Fields.Keys, Is.EquivalentTo(new[] { "TRICK_ROOM", "PSYCHIC_TERRAIN" }));
    }

    [Test]
    public void Test_TryApplyMessage_ShouldRemoveField_WhenFieldEnds()
    {
        // Arrange
        StartBattle();
        Apply("|-fieldstart|move: Trick Room|[of] p2a: Tinkaton");

        // Act
        Apply("|-fieldend|move: Trick Room");

        // Assert
        Assert.That(Tracked.Fields, Is.Empty);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldTrackSideConditions_WhenSetOnEitherSide()
    {
        // Arrange
        StartBattle();
        Apply("|turn|3");

        // Act
        Apply("|-sidestart|p1: Elsa|Reflect");
        Apply("|-sidestart|p1: Elsa|move: Light Screen");
        Apply("|-sidestart|p2: Rival|Spikes");
        Apply("|-sidestart|p2: Rival|Spikes");

        // Assert: non-stacking conditions remember their start turn, Spikes their layers
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Tracked.SideConditions, Is.EqualTo(new Dictionary<string, int> { ["REFLECT"] = 3, ["LIGHT_SCREEN"] = 3 }));
            Assert.That(Tracked.OpponentSideConditions, Is.EqualTo(new Dictionary<string, int> { ["SPIKES"] = 2 }));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldRemoveSideCondition_WhenSideEnds()
    {
        // Arrange
        StartBattle();
        Apply("|-sidestart|p1: Elsa|move: Stealth Rock");
        Apply("|-sidestart|p1: Elsa|Reflect");

        // Act
        Apply("|-sideend|p1: Elsa|Stealth Rock|[from] move: Rapid Spin|[of] p1a: Garchomp");

        // Assert
        Assert.That(Tracked.SideConditions.Keys, Is.EqualTo(new[] { "REFLECT" }));
    }

    [Test]
    public void Test_TryApplyMessage_ShouldSwapSideConditions_WhenCourtChangeIsUsed()
    {
        // Arrange
        StartBattle();
        Apply("|-sidestart|p1: Elsa|move: Stealth Rock");
        Apply("|-sidestart|p2: Rival|Reflect");

        // Act
        Apply("|-swapsideconditions");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Tracked.SideConditions.Keys, Is.EqualTo(new[] { "REFLECT" }));
            Assert.That(Tracked.OpponentSideConditions.Keys, Is.EqualTo(new[] { "STEALTH_ROCK" }));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldTrackOurBoosts_WhenBoostedAndUnboosted()
    {
        // Arrange
        StartBattle();

        // Act
        Apply("|-boost|p1a: Garchomp|atk|2");
        Apply("|-boost|p1a: Garchomp|atk|6");
        Apply("|-unboost|p1a: Garchomp|spe|1");

        // Assert: stages are capped at +6
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Garchomp.Boosts["atk"], Is.EqualTo(6));
            Assert.That(Garchomp.Boosts["spe"], Is.EqualTo(-1));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldResetBoosts_WhenClearedOrSwitchedOut()
    {
        // Arrange
        StartBattle();
        Apply("|-boost|p2a: Tinkaton|atk|2");
        Apply("|-boost|p1a: Garchomp|atk|2");
        Apply("|-setboost|p1a: Garchomp|def|4");

        // Act
        Apply("|-clearboost|p2a: Tinkaton");
        Apply("|switch|p1a: Pelipper|Pelipper, L86, F|250/250");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Opponent("Tinkaton").Boosts.Values, Is.All.Zero);
            Assert.That(Garchomp.Boosts.Values, Is.All.Zero);
            Assert.That(Garchomp.Active, Is.False);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldTrackVolatiles_WhenStartedAndEnded()
    {
        // Arrange
        StartBattle();

        // Act
        Apply("|-start|p1a: Garchomp|Substitute");
        Apply("|-start|p1a: Garchomp|confusion|[fatigue]");
        Apply("|-start|p1a: Garchomp|move: Leech Seed");
        Apply("|-start|p2a: Tinkaton|move: Taunt");
        Apply("|-start|p2a: Tinkaton|Salt Cure");
        Apply("|-end|p1a: Garchomp|Substitute");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Garchomp.Effects, Is.EquivalentTo(new[] { "CONFUSION", "LEECH_SEED" }));
            Assert.That(Opponent("Tinkaton").Effects, Is.EquivalentTo(new[] { "TAUNT", "SALT_CURE" }));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldClearVolatiles_WhenPokemonSwitchesOut()
    {
        // Arrange
        StartBattle();
        Apply("|-start|p1a: Garchomp|perish3|[silent]");
        Apply("|-start|p1a: Garchomp|move: Focus Energy");

        // Act
        Apply("|switch|p1a: Pelipper|Pelipper, L86, F|250/250");

        // Assert
        Assert.That(Garchomp.Effects, Is.Empty);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldTrackOpponentItem_WhenRevealedAndRemoved()
    {
        // Arrange
        StartBattle();
        Assert.That(Opponent("Tinkaton").Item, Is.EqualTo(TrackedPokemon.UNKNOWN_ITEM));

        // Act
        Apply("|-item|p2a: Tinkaton|Air Balloon");
        var revealed = Opponent("Tinkaton").Item;
        Apply("|-enditem|p2a: Tinkaton|Air Balloon");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(revealed, Is.EqualTo("airballoon"));
            Assert.That(Opponent("Tinkaton").Item, Is.Null);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldRevealItem_WhenDamageComesFromOpponentItem()
    {
        // Arrange
        StartBattle();

        // Act
        Apply("|-damage|p1a: Garchomp|233/280|[from] item: Rocky Helmet|[of] p2a: Tinkaton");

        // Assert
        Assert.That(Opponent("Tinkaton").Item, Is.EqualTo("rockyhelmet"));
    }

    [Test]
    public void Test_TryApplyMessage_ShouldRevealAbility_WhenAbilityIsAnnounced()
    {
        // Arrange
        StartBattle();
        Assert.That(Opponent("Tinkaton").Ability, Is.Null);

        // Act
        Apply("|-ability|p2a: Tinkaton|Mold Breaker");

        // Assert
        Assert.That(Opponent("Tinkaton").Ability, Is.EqualTo("moldbreaker"));
    }

    [Test]
    public void Test_TryApplyMessage_ShouldRevealAbility_WhenImmunityComesFromAbility()
    {
        // Arrange
        StartBattle("|switch|p2a: Bronzong|Bronzong, L88|100/100");
        Assert.That(Opponent("Bronzong").Ability, Is.Null);

        // Act
        Apply("|-immune|p2a: Bronzong|[from] ability: Levitate");

        // Assert
        Assert.That(Opponent("Bronzong").Ability, Is.EqualTo("levitate"));
    }

    [Test]
    public void Test_TryApplyMessage_ShouldKnowAbility_WhenSpeciesHasOnlyOne()
    {
        // Act
        StartBattle("|switch|p2a: Gholdengo|Gholdengo, L77|100/100");

        // Assert
        Assert.That(Opponent("Gholdengo").Ability, Is.EqualTo("goodasgold"));
    }

    [Test]
    public void Test_TryApplyMessage_ShouldTrackOpponentTera_WhenOpponentTerastallizes()
    {
        // Arrange
        StartBattle();

        // Act
        Apply("|-terastallize|p2a: Tinkaton|Steel");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Opponent("Tinkaton").Terastallized, Is.True);
            Assert.That(Opponent("Tinkaton").TeraType, Is.EqualTo("STEEL"));
            Assert.That(Opponent("Tinkaton").Types, Is.EqualTo(new[] { "STEEL" }));
            Assert.That(Tracked.OpponentUsedTera, Is.True);
            Assert.That(Tracked.UsedTera, Is.False);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldNotKnowOurTeraType_UntilWeTerastallize()
    {
        // Arrange: poke-env never reads the request's teraType, and minipoke was trained that way
        StartBattle();
        Assert.That(Garchomp.TeraType, Is.Null);

        // Act
        Apply("|-terastallize|p1a: Garchomp|Fire");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Garchomp.TeraType, Is.EqualTo("FIRE"));
            Assert.That(Tracked.UsedTera, Is.True);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldCountActiveTurns_WhenTurnEnds()
    {
        // Arrange
        StartBattle();

        // Act
        Apply("|turn|2");

        // Assert: the first turn after switching in is over
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Tracked.Turn, Is.EqualTo(2));
            Assert.That(Garchomp.ActiveTurns, Is.EqualTo(2));
            Assert.That(Garchomp.FirstTurn, Is.False);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldTrackRechargeAndCharging_UntilPokemonMovesAgain()
    {
        // Arrange
        StartBattle();
        Apply("|-mustrecharge|p2a: Tinkaton");
        Apply("|-prepare|p1a: Garchomp|Meteor Beam");
        var mustRecharge = Opponent("Tinkaton").MustRecharge;
        var preparing = Garchomp.Preparing;

        // Act
        Apply("|move|p2a: Tinkaton|Gigaton Hammer|p1a: Garchomp");
        Apply("|move|p1a: Garchomp|Earthquake|p2a: Tinkaton");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(mustRecharge, Is.True);
            Assert.That(preparing, Is.True);
            Assert.That(Opponent("Tinkaton").MustRecharge, Is.False);
            Assert.That(Garchomp.Preparing, Is.False);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldCountPp_WhenMovesAreUsed()
    {
        // Arrange: Zapdos's Pressure costs an extra PP
        StartBattle("|switch|p2a: Zapdos|Zapdos, L80|100/100");
        Apply("|-ability|p2a: Zapdos|Pressure");

        // Act
        Apply("|move|p1a: Garchomp|Outrage|p2a: Zapdos");
        Apply("|move|p1a: Garchomp|Swords Dance|p1a: Garchomp");
        Apply("|move|p2a: Zapdos|Hurricane|p1a: Garchomp");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Garchomp.Moves["outrage"].CurrentPp, Is.EqualTo(14));
            Assert.That(Garchomp.Moves["swordsdance"].CurrentPp, Is.EqualTo(31));
            Assert.That(Opponent("Zapdos").Moves.Keys, Is.EqualTo(new[] { "hurricane" }));
            Assert.That(Opponent("Zapdos").Moves["hurricane"].CurrentPp, Is.EqualTo(15));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldNotUsePp_WhenLockedIntoMove()
    {
        // Arrange
        StartBattle();
        Apply("|move|p1a: Garchomp|Outrage|p2a: Tinkaton");

        // Act
        Apply("|move|p1a: Garchomp|Outrage|p2a: Tinkaton|[from]lockedmove");

        // Assert
        Assert.That(Garchomp.Moves["outrage"].CurrentPp, Is.EqualTo(15));
    }

    [Test]
    public void Test_TryApplyMessage_ShouldTrackStatus_LikePokeEnv()
    {
        // Arrange
        StartBattle();
        Apply("|-status|p2a: Tinkaton|par");
        var paralyzed = Opponent("Tinkaton").Status;

        // Act: poke-env reads the status from every HP string, so one without a status clears it
        Apply("|-damage|p2a: Tinkaton|52/100");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(paralyzed, Is.EqualTo("PAR"));
            Assert.That(Opponent("Tinkaton").Status, Is.Null);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldMarkFainted_WhenPokemonFaints()
    {
        // Arrange
        StartBattle();

        // Act
        Apply("|-damage|p2a: Tinkaton|0 fnt");
        Apply("|faint|p2a: Tinkaton");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Opponent("Tinkaton").Fainted, Is.True);
            Assert.That(Opponent("Tinkaton").CurrentHpFraction, Is.Zero);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldChangeStatsButNotSpecies_WhenFormeChanges()
    {
        // Arrange
        StartBattle("|switch|p2a: Palafin|Palafin, L77, M|100/100");
        var zeroAttack = Opponent("Palafin").BaseStats["atk"];

        // Act
        Apply("|detailschange|p2a: Palafin|Palafin-Hero, L77, M");

        // Assert: the species id only follows the details on the next switch-in
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Opponent("Palafin").BaseStats["atk"], Is.GreaterThan(zeroAttack));
            Assert.That(Opponent("Palafin").Species, Is.EqualTo("palafin"));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldAddIllusionist_WhenIllusionBreaks()
    {
        // Arrange: a Zoroark disguised as Garchomp
        StartBattle("|switch|p2a: Garchomp|Garchomp, L80, M|100/100");

        // Act
        Apply("|replace|p2a: Zoroark|Zoroark, L82, M");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Tracked.OpponentTeam.Select(pokemon => pokemon.Identifier), Is.EqualTo(new[] { "p2: Garchomp", "p2: Zoroark" }));
            Assert.That(Tracked.OpponentActivePokemon.Identifier, Is.EqualTo("p2: Zoroark"));
            Assert.That(Opponent("Garchomp").Active, Is.False);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldTrackRequestState_WhenTrapped()
    {
        // Arrange
        StartBattle();

        // Act
        ApplyRequest(trapped: true, canTerastallize: "");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Tracked.Trapped, Is.True);
            Assert.That(Tracked.CanTera, Is.False);
            Assert.That(Tracked.AvailableSwitches, Is.Empty);
            Assert.That(Tracked.AvailableMoves.Select(move => move.Id),
                Is.EqualTo(new[] { "earthquake", "outrage", "stealthrock", "swordsdance" }));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldOfferOnlyHealthySwitches_WhenForcedToSwitch()
    {
        // Arrange
        StartBattle();
        Apply("|-damage|p1a: Garchomp|0 fnt");
        Apply("|faint|p1a: Garchomp");

        // Act
        ApplyRequest(forceSwitch: true,
            conditions: new Dictionary<string, string> { [GARCHOMP] = "0 fnt", [GHOLDENGO] = "0 fnt" });

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Tracked.ForceSwitch, Is.True);
            Assert.That(Tracked.AvailableMoves, Is.Empty);
            Assert.That(Tracked.AvailableSwitches.Select(pokemon => pokemon.Identifier), Is.EqualTo(new[] { PELIPPER }));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldKeepExistingOpponentTracking_WhenTrackingForMiniPoke()
    {
        // Arrange
        StartBattle();

        // Act
        Apply("|switch|p2a: Klefki|Klefki, L84, M|100/100");
        Apply("|-sidestart|p2: Rival|move: Stealth Rock");
        Apply("|-start|p2a: Klefki|move: Taunt");
        Apply("|-boost|p2a: Klefki|atk|2");

        // Assert: the state the other strategies read is unchanged
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.OpponentSideStealthRock, Is.True);
            Assert.That(_context.OpponentActiveTaunted, Is.True);
            Assert.That(_context.ActiveOpponent.Species, Is.EqualTo("Klefki"));
            Assert.That(_context.ActiveOpponent.Boosts["atk"], Is.EqualTo(2));
            Assert.That(Opponent("Klefki").Boosts["atk"], Is.EqualTo(2));
        }
    }
}
