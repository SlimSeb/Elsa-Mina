using ElsaMina.Battles;
using ElsaMina.Battles.Chat;
using ElsaMina.Battles.Results;
using ElsaMina.Core.Services.Probabilities;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles.Chat;

[TestFixture]
public class BattleChatServiceTest
{
    private const string ROOM_ID = "battle-gen9randombattle-1";
    private const double ALWAYS_REACT = 0;
    private const double NEVER_REACT = 0.99;
    private static readonly string[] CRIT_ON_OPPONENT = ["", "-crit", "p2a: Garchomp"];
    private static readonly string[] TURN = ["", "turn", "2"];

    private IRandomService _randomService;
    private BattleChatService _battleChatService;
    private BattleContext _context;

    [SetUp]
    public void SetUp()
    {
        _randomService = Substitute.For<IRandomService>();
        // Picks the last phrase, so a test sees that it comes from the pool and not only its first entry
        _randomService.RandomElement(Arg.Any<IEnumerable<string>>())
            .Returns(callInfo => callInfo.ArgAt<IEnumerable<string>>(0).Last());
        _randomService.NextDouble().Returns(ALWAYS_REACT);
        _battleChatService = new BattleChatService(_randomService);
        _context = new BattleContext(ROOM_ID) { SideId = "p1" };
    }

    [Test]
    public void Test_GetStartMessage_ShouldPickStartPhrase()
    {
        // Act
        var message = _battleChatService.GetStartMessage();

        // Assert
        Assert.That(message, Is.EqualTo(BattleChatPhrases.Start[^1]));
    }

    [TestCase(BattleOutcome.Won)]
    [TestCase(BattleOutcome.Lost)]
    [TestCase(BattleOutcome.Tied)]
    [TestCase(null)]
    public void Test_GetEndMessage_ShouldPickPhraseOfOutcome(BattleOutcome? outcome)
    {
        // Arrange
        var expectedPhrases = outcome switch
        {
            BattleOutcome.Won => BattleChatPhrases.Won,
            BattleOutcome.Lost => BattleChatPhrases.Lost,
            BattleOutcome.Tied => BattleChatPhrases.Tied,
            _ => BattleChatPhrases.Ended
        };

        // Act
        var message = _battleChatService.GetEndMessage(outcome);

        // Assert
        Assert.That(message, Is.EqualTo(expectedPhrases[^1]));
    }

    [TestCase("|-crit|p2a: Garchomp", BattleChatEvent.OwnCriticalHit)]
    [TestCase("|-crit|p1a: Garchomp", BattleChatEvent.OpponentCriticalHit)]
    [TestCase("|-miss|p1a: Garchomp|p2a: Mimikyu", BattleChatEvent.OwnMiss)]
    [TestCase("|-miss|p2a: Mimikyu|p1a: Garchomp", BattleChatEvent.OpponentMiss)]
    [TestCase("|cant|p1a: Garchomp|par", BattleChatEvent.OwnFullParalysis)]
    [TestCase("|cant|p2a: Mimikyu|par", BattleChatEvent.OpponentFullParalysis)]
    [TestCase("|cant|p1a: Garchomp|flinch", BattleChatEvent.OwnFlinch)]
    [TestCase("|cant|p2a: Mimikyu|flinch", BattleChatEvent.OpponentFlinch)]
    [TestCase("|-damage|p1a: Garchomp|50/100|[from] confusion", BattleChatEvent.OwnConfusionHit)]
    [TestCase("|-damage|p2a: Mimikyu|50/100|[from] confusion", BattleChatEvent.OpponentConfusionHit)]
    [TestCase("|-status|p1a: Garchomp|par", BattleChatEvent.OwnParalysis)]
    [TestCase("|-status|p2a: Mimikyu|par|[from] ability: Static", BattleChatEvent.OpponentParalysis)]
    [TestCase("|-status|p1a: Garchomp|brn", BattleChatEvent.OwnBurn)]
    [TestCase("|-status|p2a: Mimikyu|brn", BattleChatEvent.OpponentBurn)]
    [TestCase("|-status|p1a: Garchomp|frz", BattleChatEvent.OwnFreeze)]
    [TestCase("|-status|p2a: Mimikyu|frz", BattleChatEvent.OpponentFreeze)]
    [TestCase("|faint|p1a: Garchomp", BattleChatEvent.OwnFaint)]
    [TestCase("|faint|p2a: Mimikyu", BattleChatEvent.OpponentFaint)]
    public void Test_GetReaction_ShouldPickPhraseOfEvent_WhenLineIsEvent(string line, BattleChatEvent expected)
    {
        // Act
        var reaction = _battleChatService.GetReaction(line.Split('|'), _context);

        // Assert
        Assert.That(reaction, Is.EqualTo(BattleChatPhrases.Reactions[expected][^1]));
    }

    [TestCase("|-damage|p1a: Garchomp|50/100")]
    [TestCase("|-status|p1a: Garchomp|brn|[from] item: Flame Orb")]
    [TestCase("|-status|p1a: Garchomp|tox")]
    [TestCase("|cant|p1a: Garchomp|recharge")]
    [TestCase("|move|p1a: Garchomp|Earthquake|p2a: Mimikyu")]
    [TestCase("|upkeep")]
    public void Test_GetReaction_ShouldReturnNull_WhenLineIsNotEvent(string line)
    {
        // Act
        var reaction = _battleChatService.GetReaction(line.Split('|'), _context);

        // Assert
        Assert.That(reaction, Is.Null);
    }

    [Test]
    public void Test_GetReaction_ShouldReturnNull_WhenRollFails()
    {
        // Arrange
        _randomService.NextDouble().Returns(NEVER_REACT);

        // Act
        var reaction = _battleChatService.GetReaction(CRIT_ON_OPPONENT, _context);

        // Assert
        Assert.That(reaction, Is.Null);
    }

    [Test]
    public void Test_GetReaction_ShouldReturnNull_WhenBotOnlySpectatesTheBattle()
    {
        // Arrange
        _context.SideId = "";

        // Act
        var reaction = _battleChatService.GetReaction(CRIT_ON_OPPONENT, _context);

        // Assert
        Assert.That(reaction, Is.Null);
    }

    [Test]
    public void Test_GetReaction_ShouldReturnNull_WhenBotAlreadyReactedThisTurn()
    {
        // Arrange
        _battleChatService.GetReaction(CRIT_ON_OPPONENT, _context);

        // Act
        var reaction = _battleChatService.GetReaction(CRIT_ON_OPPONENT, _context);

        // Assert
        Assert.That(reaction, Is.Null);
    }

    [Test]
    public void Test_GetReaction_ShouldReactAgain_WhenNextTurnStarts()
    {
        // Arrange
        _battleChatService.GetReaction(CRIT_ON_OPPONENT, _context);
        _battleChatService.GetReaction(TURN, _context);

        // Act
        var reaction = _battleChatService.GetReaction(CRIT_ON_OPPONENT, _context);

        // Assert
        Assert.That(reaction, Is.Not.Null);
    }

    [Test]
    public void Test_Reactions_ShouldHavePhrases_ForEveryEvent()
    {
        // Assert
        Assert.That(Enum.GetValues<BattleChatEvent>(),
            Has.All.Matches<BattleChatEvent>(chatEvent => BattleChatPhrases.Reactions[chatEvent].Count > 0));
    }

    [Test]
    public void Test_Phrases_ShouldNotStartWithCommandCharacter()
    {
        // Arrange
        var phrases = BattleChatPhrases.Start
            .Concat(BattleChatPhrases.Won)
            .Concat(BattleChatPhrases.Lost)
            .Concat(BattleChatPhrases.Tied)
            .Concat(BattleChatPhrases.Ended)
            .Concat(BattleChatPhrases.Reactions.Values.SelectMany(pool => pool));

        // Assert
        Assert.That(phrases, Has.None.Match(@"^[/!]"));
    }
}
