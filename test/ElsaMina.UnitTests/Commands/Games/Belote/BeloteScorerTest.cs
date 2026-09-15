using ElsaMina.Commands.Games.Belote;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.Belote;

[TestFixture]
public class BeloteScorerTest
{
    private static readonly int[] ExpectedContractMadeDeltas = [100, 62, 100, 62];

    private static List<BelotePlayer> Players()
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
        var result = BeloteScorer.Compute(new BeloteScoreInput
        {
            TakerTeam = 0,
            Team0CardPoints = 90,
            Team1CardPoints = 62,
            LastTrickTeam = 0,
            Team0Tricks = 5,
            Team1Tricks = 3,
            BeloteTeam = -1,
            Players = Players()
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Made, Is.True);
            Assert.That(result.IsCapot, Is.False);
            Assert.That(result.Team0Score, Is.EqualTo(100)); // 90 + 10 der
            Assert.That(result.Team1Score, Is.EqualTo(62));
            Assert.That(result.Deltas, Is.EqualTo(ExpectedContractMadeDeltas));
        }
    }

    [Test]
    public void Test_Compute_ShouldFailContract_WhenTakerFallsShort()
    {
        var result = BeloteScorer.Compute(new BeloteScoreInput
        {
            TakerTeam = 0,
            Team0CardPoints = 40,
            Team1CardPoints = 112,
            LastTrickTeam = 1,
            Team0Tricks = 2,
            Team1Tricks = 6,
            BeloteTeam = -1,
            Players = Players()
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Made, Is.False);
            Assert.That(result.Team0Score, Is.Zero);
            Assert.That(result.Team1Score, Is.EqualTo(162)); // defenders take everything
        }
    }

    [Test]
    public void Test_Compute_ShouldFailContract_WhenScoresAreTied()
    {
        // 81 each after the dix de der: the taker must strictly exceed the defenders.
        var result = BeloteScorer.Compute(new BeloteScoreInput
        {
            TakerTeam = 0,
            Team0CardPoints = 81,
            Team1CardPoints = 71,
            LastTrickTeam = 1,
            Team0Tricks = 4,
            Team1Tricks = 4,
            BeloteTeam = -1,
            Players = Players()
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Made, Is.False);
            Assert.That(result.Team0Score, Is.Zero);
            Assert.That(result.Team1Score, Is.EqualTo(162));
        }
    }

    [Test]
    public void Test_Compute_ShouldScoreCapot_WhenTakerWinsEveryTrick()
    {
        var result = BeloteScorer.Compute(new BeloteScoreInput
        {
            TakerTeam = 0,
            Team0CardPoints = 152,
            Team1CardPoints = 0,
            LastTrickTeam = 0,
            Team0Tricks = 8,
            Team1Tricks = 0,
            BeloteTeam = -1,
            Players = Players()
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Made, Is.True);
            Assert.That(result.IsCapot, Is.True);
            Assert.That(result.Team0Score, Is.EqualTo(BeloteConstants.CAPOT_SCORE));
            Assert.That(result.Team1Score, Is.Zero);
        }
    }

    [Test]
    public void Test_Compute_ShouldScoreCapot_WhenDefendersWinEveryTrick()
    {
        var result = BeloteScorer.Compute(new BeloteScoreInput
        {
            TakerTeam = 0,
            Team0CardPoints = 0,
            Team1CardPoints = 152,
            LastTrickTeam = 1,
            Team0Tricks = 0,
            Team1Tricks = 8,
            BeloteTeam = -1,
            Players = Players()
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Made, Is.False);
            Assert.That(result.IsCapot, Is.True);
            Assert.That(result.Team0Score, Is.Zero);
            Assert.That(result.Team1Score, Is.EqualTo(BeloteConstants.CAPOT_SCORE));
        }
    }

    [Test]
    public void Test_Compute_ShouldAddBeloteBonus_ToTheHoldingTeam()
    {
        var result = BeloteScorer.Compute(new BeloteScoreInput
        {
            TakerTeam = 0,
            Team0CardPoints = 90,
            Team1CardPoints = 62,
            LastTrickTeam = 0,
            Team0Tricks = 5,
            Team1Tricks = 3,
            BeloteTeam = 1,
            Players = Players()
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Team0Score, Is.EqualTo(100));
            Assert.That(result.Team1Score, Is.EqualTo(82)); // 62 + 20 belote
            Assert.That(result.BeloteTeam, Is.EqualTo(1));
        }
    }
}