using ElsaMina.Commands.Games.Belote;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Belote;

[TestFixture]
public class BeloteScorerTest
{
    private static IReadOnlyList<BelotePlayer> Players()
    {
        var players = new List<BelotePlayer>();
        for (var seat = 0; seat < 4; seat++)
        {
            var user = Substitute.For<IUser>();
            user.UserId.Returns($"player{seat}");
            user.Name.Returns($"player{seat}");
            players.Add(new BelotePlayer(user) { Team = seat % 2 });
        }

        return players;
    }

    [Test]
    public void Test_Compute_ShouldMakeContract_WhenTakerOutscoresDefenders()
    {
        // Taker team 0 has 90 card points + the dix de der; defenders 62.
        var result = BeloteScorer.Compute(0, 90, 62, lastTrickTeam: 0,
            team0Tricks: 5, team1Tricks: 3, beloteTeam: -1, Players());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Made, Is.True);
            Assert.That(result.IsCapot, Is.False);
            Assert.That(result.Team0Score, Is.EqualTo(100)); // 90 + 10 der
            Assert.That(result.Team1Score, Is.EqualTo(62));
            Assert.That(result.Deltas, Is.EqualTo(new[] { 100, 62, 100, 62 }));
        }
    }

    [Test]
    public void Test_Compute_ShouldFailContract_WhenTakerFallsShort()
    {
        var result = BeloteScorer.Compute(0, 40, 112, lastTrickTeam: 1,
            team0Tricks: 2, team1Tricks: 6, beloteTeam: -1, Players());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Made, Is.False);
            Assert.That(result.Team0Score, Is.EqualTo(0));
            Assert.That(result.Team1Score, Is.EqualTo(162)); // defenders take everything
        }
    }

    [Test]
    public void Test_Compute_ShouldFailContract_WhenScoresAreTied()
    {
        // 81 each after the dix de der: the taker must strictly exceed the defenders.
        var result = BeloteScorer.Compute(0, 81, 71, lastTrickTeam: 1,
            team0Tricks: 4, team1Tricks: 4, beloteTeam: -1, Players());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Made, Is.False);
            Assert.That(result.Team0Score, Is.EqualTo(0));
            Assert.That(result.Team1Score, Is.EqualTo(162));
        }
    }

    [Test]
    public void Test_Compute_ShouldScoreCapot_WhenTakerWinsEveryTrick()
    {
        var result = BeloteScorer.Compute(0, 152, 0, lastTrickTeam: 0,
            team0Tricks: 8, team1Tricks: 0, beloteTeam: -1, Players());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Made, Is.True);
            Assert.That(result.IsCapot, Is.True);
            Assert.That(result.Team0Score, Is.EqualTo(BeloteConstants.CAPOT_SCORE));
            Assert.That(result.Team1Score, Is.EqualTo(0));
        }
    }

    [Test]
    public void Test_Compute_ShouldScoreCapot_WhenDefendersWinEveryTrick()
    {
        var result = BeloteScorer.Compute(0, 0, 152, lastTrickTeam: 1,
            team0Tricks: 0, team1Tricks: 8, beloteTeam: -1, Players());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Made, Is.False);
            Assert.That(result.IsCapot, Is.True);
            Assert.That(result.Team0Score, Is.EqualTo(0));
            Assert.That(result.Team1Score, Is.EqualTo(BeloteConstants.CAPOT_SCORE));
        }
    }

    [Test]
    public void Test_Compute_ShouldAddBeloteBonus_ToTheHoldingTeam()
    {
        var result = BeloteScorer.Compute(0, 90, 62, lastTrickTeam: 0,
            team0Tricks: 5, team1Tricks: 3, beloteTeam: 1, Players());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Team0Score, Is.EqualTo(100));
            Assert.That(result.Team1Score, Is.EqualTo(82)); // 62 + 20 belote
            Assert.That(result.BeloteTeam, Is.EqualTo(1));
        }
    }
}