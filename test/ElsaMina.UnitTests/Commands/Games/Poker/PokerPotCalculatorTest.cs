using ElsaMina.Commands.Games.Poker;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Poker;

[TestFixture]
public class PokerPotCalculatorTest
{
    private static readonly string[] ALL_THREE_PLAYERS = ["a", "b", "c"];
    private static readonly string[] PLAYERS_BAND_C = ["b", "c"];
    private static readonly string[] PLAYER_B_ONLY = ["b"];

    private static PokerPlayer Player(string id, long committed, bool folded = false)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(id);
        user.Name.Returns(id);
        return new PokerPlayer(user, 0) { Committed = committed, HasFolded = folded };
    }

    [Test]
    public void Test_BuildPots_ShouldBuildSinglePot_WhenEveryoneCommittedTheSame()
    {
        var players = new[] { Player("a", 50), Player("b", 50), Player("c", 50) };

        var pots = PokerPotCalculator.BuildPots(players);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pots, Has.Count.EqualTo(1));
            Assert.That(pots[0].Amount, Is.EqualTo(150));
            Assert.That(pots[0].EligiblePlayerIds, Is.EquivalentTo(ALL_THREE_PLAYERS));
        }
    }

    [Test]
    public void Test_BuildPots_ShouldExcludeFoldedPlayersFromEligibility()
    {
        var players = new[] { Player("a", 50, folded: true), Player("b", 50), Player("c", 50) };

        var pots = PokerPotCalculator.BuildPots(players);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pots, Has.Count.EqualTo(1));
            Assert.That(pots[0].Amount, Is.EqualTo(150));
            Assert.That(pots[0].EligiblePlayerIds, Is.EquivalentTo(PLAYERS_BAND_C));
        }
    }

    [Test]
    public void Test_BuildPots_ShouldCreateSidePot_WhenAPlayerIsAllInForLess()
    {
        var players = new[] { Player("a", 30), Player("b", 100), Player("c", 100) };

        var pots = PokerPotCalculator.BuildPots(players);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pots, Has.Count.EqualTo(2));

            Assert.That(pots[0].Amount, Is.EqualTo(90));
            Assert.That(pots[0].EligiblePlayerIds, Is.EquivalentTo(ALL_THREE_PLAYERS));

            Assert.That(pots[1].Amount, Is.EqualTo(140));
            Assert.That(pots[1].EligiblePlayerIds, Is.EquivalentTo(PLAYERS_BAND_C));
        }
    }

    [Test]
    public void Test_BuildPots_ShouldMergeFoldedOnlyLayerIntoPreviousPot()
    {
        var players = new[] { Player("a", 100, folded: true), Player("b", 40) };

        var pots = PokerPotCalculator.BuildPots(players);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pots, Has.Count.EqualTo(1));
            Assert.That(pots[0].Amount, Is.EqualTo(140));
            Assert.That(pots[0].EligiblePlayerIds, Is.EquivalentTo(PLAYER_B_ONLY));
        }
    }

    [Test]
    public void Test_BuildPots_ShouldIgnorePlayersWithoutContribution()
    {
        var players = new[] { Player("a", 0), Player("b", 20), Player("c", 20) };

        var pots = PokerPotCalculator.BuildPots(players);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pots, Has.Count.EqualTo(1));
            Assert.That(pots[0].Amount, Is.EqualTo(40));
            Assert.That(pots[0].EligiblePlayerIds, Is.EquivalentTo(PLAYERS_BAND_C));
        }
    }
}
