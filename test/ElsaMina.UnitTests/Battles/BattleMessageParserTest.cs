using ElsaMina.Battles;
using ElsaMina.Battles.Tracking;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles;

public class BattleMessageParserTest
{
    private static readonly bool[] FIRST_SLOT_FORCED_TO_SWITCH = [true];
    private static readonly string[] GARCHOMP_MOVES = ["earthquake", "swordsdance"];

    private const string ROOM_ID = "battle-gen9ou-123456";

    private BattleMessageParser _parser;
    private BattleContext _context;

    [SetUp]
    public void SetUp()
    {
        _parser = new BattleMessageParser(Substitute.For<IBattleDex>());
        _context = new BattleContext(ROOM_ID) { SideId = "p1" };
    }

    private bool Apply(string message, out BattleMessageResult result)
    {
        return _parser.TryApplyMessage(message.Split('|'), ROOM_ID, _context, out result);
    }

    private void Apply(string message)
    {
        Apply(message, out _);
    }

    private OpponentPokemonState GetOpponent(string species)
    {
        return _context.OpponentPokemon.Single(pokemon => pokemon.Species == species);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldReturnFalse_WhenMessageHasLessThanTwoParts()
    {
        // Act
        var applied = _parser.TryApplyMessage([""], ROOM_ID, _context, out var result);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Is.False);
            Assert.That(result.Type, Is.EqualTo(BattleMessageType.None));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldReturnBattleStarted_WhenStartMessage()
    {
        // Act
        var applied = Apply("|start", out var result);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Is.True);
            Assert.That(result.Type, Is.EqualTo(BattleMessageType.BattleStarted));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldReturnBattleEndedWithWinner_WhenWinMessage()
    {
        // Act
        var applied = Apply("|win|Slim", out var result);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Is.True);
            Assert.That(result.Type, Is.EqualTo(BattleMessageType.BattleEnded));
            Assert.That(result.WinnerName, Is.EqualTo("Slim"));
            Assert.That(result.IsTie, Is.False);
            Assert.That(_context.IsBattleOver, Is.True);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldReturnTie_WhenTieMessage()
    {
        // Act
        var applied = Apply("|tie", out var result);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Is.True);
            Assert.That(result.Type, Is.EqualTo(BattleMessageType.BattleEnded));
            Assert.That(result.WinnerName, Is.Null);
            Assert.That(result.IsTie, Is.True);
            Assert.That(_context.IsBattleOver, Is.True);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldReturnBattleEndedWithoutWinner_WhenWinMessageHasNoName()
    {
        // Act
        Apply("|win", out var result);

        // Assert
        Assert.That(result.WinnerName, Is.Null);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldReturnFalse_WhenMessageTypeIsUnknown()
    {
        // Act
        var applied = Apply("|j|Someone", out var result);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Is.False);
            Assert.That(result.Type, Is.EqualTo(BattleMessageType.None));
        }
    }

    [TestCase("|request")]
    [TestCase("|request|")]
    [TestCase("|request|null")]
    [TestCase("|request|   ")]
    public void Test_TryApplyMessage_ShouldReturnFalse_WhenRequestIsEmpty(string message)
    {
        // Act
        var applied = Apply(message, out _);

        // Assert
        Assert.That(applied, Is.False);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldReturnFalse_WhenRequestJsonIsInvalid()
    {
        // Act
        var applied = Apply("|request|{not json", out var result);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Is.False);
            Assert.That(result.Type, Is.EqualTo(BattleMessageType.None));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldFillContext_WhenRequestIsValid()
    {
        // Arrange
        const string json = """
            {
              "active": [{
                "moves": [
                  {"move": "Earthquake", "id": "earthquake", "pp": 16, "maxpp": 16, "target": "allAdjacent", "disabled": false},
                  {"move": "Swords Dance", "id": "swordsdance", "pp": "3", "maxpp": 32, "target": "self", "disabled": true}
                ],
                "canTerastallize": "Ground",
                "trapped": true
              }],
              "side": {
                "name": "Slim",
                "id": "p2",
                "pokemon": [
                  {
                    "ident": "p2: Garchomp",
                    "details": "Garchomp, L80, M",
                    "condition": "168/216 brn",
                    "active": true,
                    "stats": {"atk": 200, "def": 180, "spa": 150, "spd": 160, "spe": 190},
                    "moves": ["earthquake", "swordsdance"],
                    "baseAbility": "roughskin",
                    "ability": "roughskin",
                    "item": "choicescarf",
                    "pokeball": "pokeball",
                    "teraType": "Ground",
                    "terastallized": "",
                    "commanding": false,
                    "reviving": false
                  },
                  {
                    "ident": "p2: Pikachu",
                    "details": "Pikachu, L90",
                    "condition": "0 fnt",
                    "active": false,
                    "stats": {"atk": 100, "def": 80, "spa": 90, "spd": 90, "spe": 150},
                    "moves": ["thunderbolt"]
                  }
                ]
              },
              "noCancel": true,
              "rqid": 7,
            }
            """;

        // Act
        var applied = Apply("|request|" + json.Replace('\n', ' '), out var result);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Is.True);
            Assert.That(result.Type, Is.EqualTo(BattleMessageType.RequestUpdated));
            Assert.That(_context.Rqid, Is.EqualTo(7));
            Assert.That(_context.Wait, Is.False);
            Assert.That(_context.TeamPreview, Is.False);
            Assert.That(_context.SideId, Is.EqualTo("p2"));
            Assert.That(_context.ForceSwitchSlots, Is.Empty);

            Assert.That(_context.SidePokemon, Has.Count.EqualTo(2));
            var garchomp = _context.SidePokemon[0];
            Assert.That(garchomp.Ident, Is.EqualTo("p2: Garchomp"));
            Assert.That(garchomp.Details, Is.EqualTo("Garchomp, L80, M"));
            Assert.That(garchomp.CurrentHp, Is.EqualTo(168));
            Assert.That(garchomp.MaxHp, Is.EqualTo(216));
            Assert.That(garchomp.IsActive, Is.True);
            Assert.That(garchomp.IsFainted, Is.False);
            Assert.That(garchomp.Stats, Is.EqualTo(new BattlePokemonStats(200, 180, 150, 160, 190)));
            Assert.That(garchomp.Moves, Is.EqualTo(GARCHOMP_MOVES));
            Assert.That(garchomp.Item, Is.EqualTo("choicescarf"));
            Assert.That(garchomp.Ability, Is.EqualTo("roughskin"));
            var pikachu = _context.SidePokemon[1];
            Assert.That(pikachu.IsFainted, Is.True);
            Assert.That(pikachu.CurrentHp, Is.Zero);
            Assert.That(pikachu.MaxHp, Is.Zero);

            Assert.That(_context.ActiveSlots, Has.Count.EqualTo(1));
            var slot = _context.ActiveSlots[0];
            Assert.That(slot.CanTerastallize, Is.EqualTo("Ground"));
            Assert.That(slot.Trapped, Is.True);
            Assert.That(slot.Moves, Has.Count.EqualTo(2));
            Assert.That(slot.Moves[0].Name, Is.EqualTo("Earthquake"));
            Assert.That(slot.Moves[0].Id, Is.EqualTo("earthquake"));
            Assert.That(slot.Moves[0].Pp, Is.EqualTo(16));
            Assert.That(slot.Moves[1].Pp, Is.EqualTo(3));
            Assert.That(slot.Moves[1].MaxPp, Is.EqualTo(32));
            Assert.That(slot.Moves[1].IsDisabled, Is.True);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldParseForceSwitchAndEmptyActiveSlot_WhenRequestForcesSwitch()
    {
        // Arrange
        const string json = """{"forceSwitch":[true],"active":[{"moves":[]}],"side":{"name":"Slim","id":"p1","pokemon":[]},"rqid":3}""";

        // Act
        Apply("|request|" + json);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.ForceSwitchSlots, Is.EqualTo(FIRST_SLOT_FORCED_TO_SWITCH));
            Assert.That(_context.SidePokemon, Is.Empty);
            Assert.That(_context.ActiveSlots, Has.Count.EqualTo(1));
            Assert.That(_context.ActiveSlots[0].Moves, Is.Empty);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldParseWaitAndTeamPreview_WhenRequestHasNoActiveSlots()
    {
        // Arrange
        const string json = """{"wait":true,"teamPreview":true,"active":[],"side":{"name":"Slim","id":"p1"},"rqid":1}""";

        // Act
        Apply("|request|" + json);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.Wait, Is.True);
            Assert.That(_context.TeamPreview, Is.True);
            Assert.That(_context.ActiveSlots, Is.Empty);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreOpponentMessages_WhenSideIdIsUnknown()
    {
        // Arrange
        _context.SideId = "";

        // Act
        var applied = Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100", out _);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Is.False);
            Assert.That(_context.OpponentPokemon, Is.Empty);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldRegisterOpponentPokemon_WhenTeamPreviewOfOpponent()
    {
        // Act
        var applied = Apply("|poke|p2|Garchomp, L80, F|item", out _);

        // Assert
        var garchomp = GetOpponent("Garchomp");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(applied, Is.False);
            Assert.That(garchomp.Level, Is.EqualTo(80));
            Assert.That(garchomp.IsActive, Is.False);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreTeamPreview_WhenPokemonIsOurs()
    {
        // Act
        Apply("|poke|p1|Garchomp, L80, M|item");

        // Assert
        Assert.That(_context.OpponentPokemon, Is.Empty);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldDefaultToLevelHundred_WhenDetailsHaveNoLevel()
    {
        // Act
        Apply("|poke|p2|Sunflora, M|");

        // Assert
        Assert.That(GetOpponent("Sunflora").Level, Is.EqualTo(100));
    }

    [TestCase("switch")]
    [TestCase("drag")]
    [TestCase("replace")]
    public void Test_TryApplyMessage_ShouldTrackActiveOpponent_WhenOpponentSwitchesIn(string switchType)
    {
        // Act
        Apply($"|{switchType}|p2a: Garchomp|Garchomp, L80, M|88/100 brn");

        // Assert
        var garchomp = GetOpponent("Garchomp");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(garchomp.IsActive, Is.True);
            Assert.That(garchomp.IsFainted, Is.False);
            Assert.That(garchomp.HpPercent, Is.EqualTo(88).Within(0.001));
            Assert.That(garchomp.Status, Is.EqualTo("brn"));
            Assert.That(garchomp.Level, Is.EqualTo(80));
            Assert.That(_context.ActiveOpponent, Is.SameAs(garchomp));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldDeactivatePreviousOpponentAndResetState_WhenOpponentSwitches()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100");
        Apply("|-boost|p2a: Garchomp|atk|2");
        Apply("|-start|p2a: Garchomp|move: Taunt");

        // Act
        Apply("|switch|p2a: Pikachu|Pikachu, L90|50/200");

        // Assert
        var garchomp = GetOpponent("Garchomp");
        var pikachu = GetOpponent("Pikachu");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(garchomp.IsActive, Is.False);
            Assert.That(pikachu.IsActive, Is.True);
            Assert.That(pikachu.HpPercent, Is.EqualTo(25).Within(0.001));
            Assert.That(_context.OpponentActiveTaunted, Is.False);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldClearBoosts_WhenOpponentSwitchesBackIn()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100");
        Apply("|-boost|p2a: Garchomp|atk|2");

        // Act
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100");

        // Assert
        Assert.That(GetOpponent("Garchomp").Boosts, Is.Empty);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldMarkFainted_WhenOpponentSwitchesInFainted()
    {
        // Act
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|0 fnt");

        // Assert
        var garchomp = GetOpponent("Garchomp");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(garchomp.IsFainted, Is.True);
            Assert.That(garchomp.IsActive, Is.False);
            Assert.That(garchomp.HpPercent, Is.Zero);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreSwitch_WhenOurPokemonSwitches()
    {
        // Act
        Apply("|switch|p1a: Garchomp|Garchomp, L80, M|100/100");

        // Assert
        Assert.That(_context.OpponentPokemon, Is.Empty);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldKeepZeroHp_WhenHpStatusIsEmpty()
    {
        // Act
        Apply("|switch|p2a: Garchomp|Garchomp|");

        // Assert
        Assert.That(GetOpponent("Garchomp").HpPercent, Is.Zero);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldKeepZeroHp_WhenHpHasNoMaximum()
    {
        // Act
        Apply("|switch|p2a: Garchomp|Garchomp|88");

        // Assert
        Assert.That(GetOpponent("Garchomp").HpPercent, Is.Zero);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldRenameSpecies_WhenOpponentChangesForme()
    {
        // Arrange
        Apply("|switch|p2a: Mimikyu|Mimikyu, L79, F|100/100");

        // Act
        Apply("|detailschange|p2a: Mimikyu|Mimikyu-Busted, L79, F");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.OpponentPokemon, Has.Count.EqualTo(1));
            Assert.That(_context.OpponentPokemon[0].Species, Is.EqualTo("Mimikyu-Busted"));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldDoNothing_WhenFormeChangeTargetsUnknownPokemon()
    {
        // Act
        Apply("|detailschange|p2a: Mimikyu|Mimikyu-Busted, L79, F");

        // Assert
        Assert.That(_context.OpponentPokemon, Is.Empty);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreFormeChange_WhenOurPokemonChanges()
    {
        // Arrange
        Apply("|switch|p2a: Mimikyu|Mimikyu, L79, F|100/100");

        // Act
        Apply("|detailschange|p1a: Mimikyu|Mimikyu-Busted, L79, F");

        // Assert
        Assert.That(_context.OpponentPokemon[0].Species, Is.EqualTo("Mimikyu"));
    }

    [Test]
    public void Test_TryApplyMessage_ShouldRecordRevealedMoves_WhenOpponentUsesMoves()
    {
        // Act
        Apply("|move|p2a: Garchomp|Earthquake|p1a: Mimikyu");
        Apply("|move|p2a: Garchomp|Swords Dance|p2a: Garchomp");
        Apply("|move|p2a: Garchomp|earthquake|p1a: Mimikyu");

        // Assert
        var garchomp = GetOpponent("Garchomp");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(garchomp.LastUsedMove, Is.EqualTo("earthquake"));
            Assert.That(garchomp.RevealedMoves, Has.Count.EqualTo(2));
            Assert.That(garchomp.IsActive, Is.True);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldNotOverrideActiveOpponent_WhenAnotherOpponentUsesMove()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100");

        // Act
        Apply("|move|p2a: Pikachu|Thunderbolt|p1a: Mimikyu");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GetOpponent("Pikachu").IsActive, Is.False);
            Assert.That(GetOpponent("Garchomp").IsActive, Is.True);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreMove_WhenOurPokemonUsesIt()
    {
        // Act
        Apply("|move|p1a: Garchomp|Earthquake|p2a: Mimikyu");

        // Assert
        Assert.That(_context.OpponentPokemon, Is.Empty);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldUpdateHpAndStatus_WhenOpponentTakesDamage()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100");

        // Act
        Apply("|-damage|p2a: Garchomp|64/100 psn");

        // Assert
        var garchomp = GetOpponent("Garchomp");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(garchomp.HpPercent, Is.EqualTo(64).Within(0.001));
            Assert.That(garchomp.Status, Is.EqualTo("psn"));
            Assert.That(garchomp.IsActive, Is.True);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldKeepStatus_WhenDamageHasNoStatus()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100 brn");

        // Act
        Apply("|-damage|p2a: Garchomp|50/100");

        // Assert
        Assert.That(GetOpponent("Garchomp").Status, Is.EqualTo("brn"));
    }

    [Test]
    public void Test_TryApplyMessage_ShouldMarkFainted_WhenDamageKnocksOut()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100");

        // Act
        Apply("|-damage|p2a: Garchomp|0 fnt");

        // Assert
        var garchomp = GetOpponent("Garchomp");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(garchomp.IsFainted, Is.True);
            Assert.That(garchomp.IsActive, Is.False);
            Assert.That(garchomp.HpPercent, Is.Zero);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreDamage_WhenOurPokemonIsHit()
    {
        // Act
        Apply("|-damage|p1a: Garchomp|50/100");

        // Assert
        Assert.That(_context.OpponentPokemon, Is.Empty);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldUpdateHp_WhenOpponentHeals()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|40/100");

        // Act
        Apply("|-heal|p2a: Garchomp|80/100");

        // Assert
        Assert.That(GetOpponent("Garchomp").HpPercent, Is.EqualTo(80).Within(0.001));
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreHeal_WhenOurPokemonHeals()
    {
        // Act
        Apply("|-heal|p1a: Garchomp|80/100");

        // Assert
        Assert.That(_context.OpponentPokemon, Is.Empty);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldMarkFainted_WhenOpponentFaints()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|30/100");

        // Act
        Apply("|faint|p2a: Garchomp");

        // Assert
        var garchomp = GetOpponent("Garchomp");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(garchomp.IsFainted, Is.True);
            Assert.That(garchomp.IsActive, Is.False);
            Assert.That(garchomp.HpPercent, Is.Zero);
            Assert.That(_context.ActiveOpponent, Is.Null);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldDoNothing_WhenUnknownOpponentFaints()
    {
        // Act
        Apply("|faint|p2a: Garchomp");

        // Assert
        Assert.That(_context.OpponentPokemon, Is.Empty);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreFaint_WhenOurPokemonFaints()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|30/100");

        // Act
        Apply("|faint|p1a: Garchomp");

        // Assert
        Assert.That(GetOpponent("Garchomp").IsFainted, Is.False);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldSetAndCureStatus_WhenOpponentStatusChanges()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100");

        // Act
        Apply("|-status|p2a: Garchomp|par");
        var statusAfterApply = GetOpponent("Garchomp").Status;
        Apply("|-curestatus|p2a: Garchomp|par");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(statusAfterApply, Is.EqualTo("par"));
            Assert.That(GetOpponent("Garchomp").Status, Is.Empty);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreStatus_WhenOpponentIsUnknownOrOurs()
    {
        // Act
        Apply("|-status|p2a: Garchomp|par");
        Apply("|-status|p1a: Garchomp|par");

        // Assert
        Assert.That(_context.OpponentPokemon, Is.Empty);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldClampBoosts_WhenOpponentBoostsAndUnboosts()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100");

        // Act
        Apply("|-boost|p2a: Garchomp|atk|4");
        Apply("|-boost|p2a: Garchomp|atk|4");
        Apply("|-unboost|p2a: Garchomp|spe|2");
        Apply("|-unboost|p2a: Garchomp|def|8");

        // Assert
        var boosts = GetOpponent("Garchomp").Boosts;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(boosts["atk"], Is.EqualTo(6));
            Assert.That(boosts["spe"], Is.EqualTo(-2));
            Assert.That(boosts["def"], Is.EqualTo(-6));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreBoost_WhenAmountIsInvalidOrPokemonUnknown()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100");

        // Act
        Apply("|-boost|p2a: Garchomp|atk|lots");
        Apply("|-boost|p2a: Pikachu|atk|2");
        Apply("|-boost|p1a: Garchomp|atk|2");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GetOpponent("Garchomp").Boosts, Is.Empty);
            Assert.That(_context.OpponentPokemon, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldClearOnePokemonBoosts_WhenClearBoost()
    {
        // Arrange
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100");
        Apply("|-boost|p2a: Garchomp|atk|2");

        // Act
        Apply("|-clearboost|p1a: Garchomp");
        var boostsAfterOwnClear = GetOpponent("Garchomp").Boosts.Count;
        Apply("|-clearboost|p2a: Garchomp");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(boostsAfterOwnClear, Is.EqualTo(1));
            Assert.That(GetOpponent("Garchomp").Boosts, Is.Empty);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldClearAllOpponentBoosts_WhenClearAllBoost()
    {
        // Arrange
        Apply("|poke|p2|Pikachu, L90|");
        Apply("|switch|p2a: Garchomp|Garchomp, L80, M|100/100");
        Apply("|-boost|p2a: Garchomp|atk|2");
        GetOpponent("Pikachu").Boosts["spe"] = 1;

        // Act
        Apply("|-clearallboost");

        // Assert
        Assert.That(_context.OpponentPokemon.All(pokemon => pokemon.Boosts.Count == 0), Is.True);
    }

    [TestCase("move: Taunt")]
    [TestCase("Taunt")]
    public void Test_TryApplyMessage_ShouldTrackTaunt_WhenOpponentIsTauntedAndRecovers(string condition)
    {
        // Act
        Apply($"|-start|p2a: Garchomp|{condition}");
        var tauntedAfterStart = _context.OpponentActiveTaunted;
        Apply($"|-end|p2a: Garchomp|{condition}");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tauntedAfterStart, Is.True);
            Assert.That(_context.OpponentActiveTaunted, Is.False);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreVolatile_WhenNotTauntOrOnOurSide()
    {
        // Act
        Apply("|-start|p2a: Garchomp|confusion");
        Apply("|-start|p1a: Garchomp|move: Taunt");

        // Assert
        Assert.That(_context.OpponentActiveTaunted, Is.False);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldClearOpponentSideHazards_WhenRemoved()
    {
        // Arrange
        Apply("|-sidestart|p2: Rival|move: Stealth Rock");
        Apply("|-sidestart|p2: Rival|Spikes");
        Apply("|-sidestart|p2: Rival|move: Toxic Spikes");
        Apply("|-sidestart|p2: Rival|move: Sticky Web");

        // Act
        Apply("|-sideend|p2: Rival|Stealth Rock|[from] move: Defog");
        Apply("|-sideend|p2: Rival|Spikes|[from] move: Defog");
        Apply("|-sideend|p2: Rival|move: Toxic Spikes|[from] move: Defog");
        Apply("|-sideend|p2: Rival|Sticky Web|[from] move: Defog");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.OpponentSideStealthRock, Is.False);
            Assert.That(_context.OpponentSideSpikesLayers, Is.Zero);
            Assert.That(_context.OpponentSideToxicSpikes, Is.False);
            Assert.That(_context.OpponentSideStickyWeb, Is.False);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreHazard_WhenSideIdentIsTooShort()
    {
        // Act
        Apply("|-sidestart|p|move: Stealth Rock");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.OwnSideStealthRock, Is.False);
            Assert.That(_context.OpponentSideStealthRock, Is.False);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreOwnSideToxicSpikesAndStickyWeb_WhenSetOnOurSide()
    {
        // Act
        Apply("|-sidestart|p1: Slim|move: Toxic Spikes");
        Apply("|-sidestart|p1: Slim|move: Sticky Web");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.OpponentSideToxicSpikes, Is.False);
            Assert.That(_context.OpponentSideStickyWeb, Is.False);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldCreateEmptySpecies_WhenIdentHasNoName()
    {
        // Act
        Apply("|move|p2a|Tackle|p1a: Garchomp");

        // Assert
        Assert.That(_context.OpponentPokemon.Single().Species, Is.Empty);
    }
}
