using ElsaMina.Commands.Games.President;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Games.President;

[TestFixture]
public class PresidentRulesTest
{
    private static PresidentCard Card(string token) => PresidentCard.Parse(token);

    private static List<PresidentCard> Hand(params string[] tokens) => tokens.Select(Card).ToList();

    private static PresidentPlayer Seat(string id)
    {
        var user = Substitute.For<IUser>();
        user.UserId.Returns(id);
        user.Name.Returns(id);
        return new PresidentPlayer(user);
    }

    /// <summary>
    /// Builds a pile from a list of plays, each written as the cards of one play.
    /// </summary>
    private static PresidentTrick Pile(params string[][] plays)
    {
        var trick = new PresidentTrick();
        foreach (var play in plays)
        {
            trick.Add(Seat("seat" + trick.Plays.Count), Hand(play));
        }

        return trick;
    }

    #region GetLegalPlays

    [Test]
    public void Test_GetLegalPlays_ShouldOfferEveryGroupSize_WhenLeading()
    {
        var plays = PresidentRules.GetLegalPlays(Hand("3h", "3s", "kd"), new PresidentTrick(),
            matchRequired: false);

        Assert.That(plays, Is.EqualTo(new[] { (3, 1), (3, 2), (PresidentCard.KING, 1) }));
    }

    [Test]
    public void Test_GetLegalPlays_ShouldBeOrderedByRank()
    {
        var plays = PresidentRules.GetLegalPlays(Hand("kd", "3h", "10s"), new PresidentTrick(),
            matchRequired: false);

        Assert.That(plays.Select(play => play.Rank), Is.Ordered);
    }

    [Test]
    public void Test_GetLegalPlays_ShouldOnlyOfferThePilesCountAtOrAboveItsRank()
    {
        var plays = PresidentRules.GetLegalPlays(Hand("3h", "3s", "kd", "kc", "5h"), Pile(["7h", "7s"]),
            matchRequired: false);

        Assert.That(plays, Is.EqualTo(new[] { (PresidentCard.KING, 2) }));
    }

    [Test]
    public void Test_GetLegalPlays_ShouldOfferTheSameRank_BecauseEqualRanksAreAllowed()
    {
        var plays = PresidentRules.GetLegalPlays(Hand("7d", "kc"), Pile(["7h"]), matchRequired: false);

        Assert.That(plays, Is.EqualTo(new[] { (7, 1), (PresidentCard.KING, 1) }));
    }

    [Test]
    public void Test_GetLegalPlays_ShouldOfferNothing_WhenNoGroupIsBigEnough()
    {
        var plays = PresidentRules.GetLegalPlays(Hand("kd", "ac"), Pile(["7h", "7s"]), matchRequired: false);

        Assert.That(plays, Is.Empty);
    }

    /// <summary>
    /// Under "ou rien" only the pile's exact rank may be played, and only when the hand can supply it.
    /// </summary>
    [Test]
    public void Test_GetLegalPlays_ShouldOnlyOfferThePilesRank_WhenOuRienApplies()
    {
        var plays = PresidentRules.GetLegalPlays(Hand("7d", "kc", "ah"), Pile(["7h"], ["7s"]),
            matchRequired: true);

        Assert.That(plays, Is.EqualTo(new[] { (7, 1) }));
    }

    [Test]
    public void Test_GetLegalPlays_ShouldOfferNothing_WhenOuRienCannotBeSatisfied()
    {
        var plays = PresidentRules.GetLegalPlays(Hand("kc", "ah"), Pile(["7h"], ["7s"]), matchRequired: true);

        Assert.That(plays, Is.Empty);
    }

    /// <summary>
    /// "Ou rien" on an empty pile is meaningless: the leader may open with anything.
    /// </summary>
    [Test]
    public void Test_GetLegalPlays_ShouldIgnoreOuRien_WhenThePileIsEmpty()
    {
        var plays = PresidentRules.GetLegalPlays(Hand("3h", "kd"), new PresidentTrick(), matchRequired: true);

        Assert.That(plays, Is.EqualTo(new[] { (3, 1), (PresidentCard.KING, 1) }));
    }

    #endregion

    #region CanMatchPile

    [Test]
    public void Test_CanMatchPile_ShouldRequireEnoughCardsOfTheExactRank()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(PresidentRules.CanMatchPile(Hand("7d", "7c"), Pile(["7h", "7s"])), Is.True);
            Assert.That(PresidentRules.CanMatchPile(Hand("7d"), Pile(["7h", "7s"])), Is.False);
            Assert.That(PresidentRules.CanMatchPile(Hand("kd", "kc"), Pile(["7h", "7s"])), Is.False);
        }
    }

    [Test]
    public void Test_CanMatchPile_ShouldBeFalse_WhenThePileIsEmpty()
    {
        Assert.That(PresidentRules.CanMatchPile(Hand("7d"), new PresidentTrick()), Is.False);
    }

    #endregion

    #region CompletesSquare

    [Test]
    public void Test_CompletesSquare_ShouldBeTrue_WhenAFourCardLeadIsPlayed()
    {
        Assert.That(PresidentRules.CompletesSquare(Pile(["7h", "7s", "7d", "7c"]), 7), Is.True);
    }

    [Test]
    public void Test_CompletesSquare_ShouldBeTrue_WhenAPairCompletesAPair()
    {
        Assert.That(PresidentRules.CompletesSquare(Pile(["7h", "7s"], ["7d", "7c"]), 7), Is.True);
    }

    [Test]
    public void Test_CompletesSquare_ShouldBeTrue_WhenAFourthSingleClosesAnOuRienChain()
    {
        Assert.That(PresidentRules.CompletesSquare(Pile(["7h"], ["7s"], ["7d"], ["7c"]), 7), Is.True);
    }

    /// <summary>
    /// Only the trailing run of the pile counts, so a lower play in between breaks the square.
    /// </summary>
    [Test]
    public void Test_CompletesSquare_ShouldOnlyCountTheTrailingRunOfTheSameRank()
    {
        Assert.That(PresidentRules.CompletesSquare(Pile(["5h"], ["7s"], ["7d"], ["7c"]), 7), Is.False);
    }

    [Test]
    public void Test_CompletesSquare_ShouldBeFalse_WhenFewerThanFourCardsAreOnThePile()
    {
        Assert.That(PresidentRules.CompletesSquare(Pile(["7h"], ["7s"]), 7), Is.False);
    }

    #endregion

    #region BeatsCurrentPlay

    [Test]
    public void Test_BeatsCurrentPlay_ShouldAcceptAnything_WhenThePileIsEmpty()
    {
        Assert.That(PresidentRules.BeatsCurrentPlay(new PresidentTrick(), 3, 1, matchRequired: false),
            Is.EqualTo(PresidentPlayRejection.None));
    }

    [Test]
    public void Test_BeatsCurrentPlay_ShouldRejectAWrongCardCountFirst()
    {
        Assert.That(PresidentRules.BeatsCurrentPlay(Pile(["7h", "7s"]), 3, 1, matchRequired: false),
            Is.EqualTo(PresidentPlayRejection.WrongCount));
    }

    [Test]
    public void Test_BeatsCurrentPlay_ShouldRejectALowerRank()
    {
        Assert.That(PresidentRules.BeatsCurrentPlay(Pile(["7h"]), 3, 1, matchRequired: false),
            Is.EqualTo(PresidentPlayRejection.TooLow));
    }

    [Test]
    public void Test_BeatsCurrentPlay_ShouldAcceptAnEqualRank()
    {
        Assert.That(PresidentRules.BeatsCurrentPlay(Pile(["7h"]), 7, 1, matchRequired: false),
            Is.EqualTo(PresidentPlayRejection.None));
    }

    [Test]
    public void Test_BeatsCurrentPlay_ShouldRejectAHigherRank_WhenOuRienApplies()
    {
        Assert.That(PresidentRules.BeatsCurrentPlay(Pile(["7h"], ["7s"]), PresidentCard.KING, 1,
            matchRequired: true), Is.EqualTo(PresidentPlayRejection.MustMatch));
    }

    [Test]
    public void Test_BeatsCurrentPlay_ShouldAcceptTheExactRank_WhenOuRienApplies()
    {
        Assert.That(PresidentRules.BeatsCurrentPlay(Pile(["7h"], ["7s"]), 7, 1, matchRequired: true),
            Is.EqualTo(PresidentPlayRejection.None));
    }

    #endregion
}
