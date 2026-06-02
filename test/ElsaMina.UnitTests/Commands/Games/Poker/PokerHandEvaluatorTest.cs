using ElsaMina.Commands.Games.Poker;

namespace ElsaMina.UnitTests.Commands.Games.Poker;

[TestFixture]
public class PokerHandEvaluatorTest
{
    private static readonly int[] ROYAL_FLUSH_TIEBREAKERS = [14];
    private static readonly int[] WHEEL_STRAIGHT_FLUSH_TIEBREAKERS = [5];
    private static readonly int[] FOUR_OF_A_KIND_TIEBREAKERS = [9, 13];
    private static readonly int[] FULL_HOUSE_TIEBREAKERS = [8, 13];
    private static readonly int[] FLUSH_TIEBREAKERS = [14, 11, 9, 5, 2];
    private static readonly int[] STRAIGHT_TIEBREAKERS = [9];
    private static readonly int[] WHEEL_STRAIGHT_TIEBREAKERS = [5];
    private static readonly int[] THREE_OF_A_KIND_TIEBREAKERS = [12, 9, 2];
    private static readonly int[] TWO_PAIR_TIEBREAKERS = [14, 7, 13];
    private static readonly int[] PAIR_TIEBREAKERS = [10, 14, 9, 3];
    private static readonly int[] HIGH_CARD_TIEBREAKERS = [14, 12, 9, 5, 3];
    private static readonly int[] BEST_FLUSH_TIEBREAKERS = [14, 13, 9, 5, 2];
    private static readonly int[] BEST_STRAIGHT_TIEBREAKERS = [9];

    private static readonly string[] BEST_OF_SEVEN_CARDS = ["Ah", "Kh", "9h", "5h", "2h", "9d", "9c"];
    private static readonly string[] STRAIGHT_OF_SEVEN_CARDS = ["9h", "8d", "5c", "6s", "7h", "2c", "Kd"];

    private static PokerCard Card(string token)
    {
        var rank = token[0] switch
        {
            'A' => 14,
            'K' => 13,
            'Q' => 12,
            'J' => 11,
            'T' => 10,
            _ => token[0] - '0'
        };
        var suit = token[1] switch
        {
            'h' => PokerSuit.Hearts,
            'd' => PokerSuit.Diamonds,
            'c' => PokerSuit.Clubs,
            _ => PokerSuit.Spades
        };
        return new PokerCard(suit, rank);
    }

    private static PokerHandEvaluation Evaluate(params string[] tokens) =>
        PokerHandEvaluator.EvaluateFive(tokens.Select(Card).ToList());

    [Test]
    public void Test_EvaluateFive_ShouldDetectRoyalFlush()
    {
        var evaluation = Evaluate("Ah", "Kh", "Qh", "Jh", "Th");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.StraightFlush));
            Assert.That(evaluation.IsRoyalFlush, Is.True);
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(ROYAL_FLUSH_TIEBREAKERS));
        }
    }

    [Test]
    public void Test_EvaluateFive_ShouldDetectWheelStraightFlush_WithFiveHigh()
    {
        var evaluation = Evaluate("5h", "4h", "3h", "2h", "Ah");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.StraightFlush));
            Assert.That(evaluation.IsRoyalFlush, Is.False);
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(WHEEL_STRAIGHT_FLUSH_TIEBREAKERS));
        }
    }

    [Test]
    public void Test_EvaluateFive_ShouldDetectFourOfAKind()
    {
        var evaluation = Evaluate("9h", "9d", "9c", "9s", "Kh");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.FourOfAKind));
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(FOUR_OF_A_KIND_TIEBREAKERS));
        }
    }

    [Test]
    public void Test_EvaluateFive_ShouldDetectFullHouse()
    {
        var evaluation = Evaluate("8h", "8d", "8c", "Ks", "Kh");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.FullHouse));
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(FULL_HOUSE_TIEBREAKERS));
        }
    }

    [Test]
    public void Test_EvaluateFive_ShouldDetectFlush_WithRanksAsTiebreakers()
    {
        var evaluation = Evaluate("Ah", "Jh", "9h", "5h", "2h");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.Flush));
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(FLUSH_TIEBREAKERS));
        }
    }

    [Test]
    public void Test_EvaluateFive_ShouldDetectStraight()
    {
        var evaluation = Evaluate("9h", "8d", "7c", "6s", "5h");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.Straight));
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(STRAIGHT_TIEBREAKERS));
        }
    }

    [Test]
    public void Test_EvaluateFive_ShouldDetectWheelStraight()
    {
        var evaluation = Evaluate("Ah", "5d", "4c", "3s", "2h");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.Straight));
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(WHEEL_STRAIGHT_TIEBREAKERS));
        }
    }

    [Test]
    public void Test_EvaluateFive_ShouldDetectThreeOfAKind()
    {
        var evaluation = Evaluate("Qh", "Qd", "Qc", "9s", "2h");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.ThreeOfAKind));
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(THREE_OF_A_KIND_TIEBREAKERS));
        }
    }

    [Test]
    public void Test_EvaluateFive_ShouldDetectTwoPair()
    {
        var evaluation = Evaluate("Ah", "Ad", "7c", "7s", "Kh");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.TwoPair));
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(TWO_PAIR_TIEBREAKERS));
        }
    }

    [Test]
    public void Test_EvaluateFive_ShouldDetectPair()
    {
        var evaluation = Evaluate("Th", "Td", "Ac", "9s", "3h");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.Pair));
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(PAIR_TIEBREAKERS));
        }
    }

    [Test]
    public void Test_EvaluateFive_ShouldDetectHighCard()
    {
        var evaluation = Evaluate("Ah", "Qd", "9c", "5s", "3h");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.HighCard));
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(HIGH_CARD_TIEBREAKERS));
        }
    }

    [Test]
    public void Test_StraightFlush_ShouldBeatFourOfAKind()
    {
        var straightFlush = Evaluate("9h", "8h", "7h", "6h", "5h");
        var fourOfAKind = Evaluate("Ah", "Ad", "Ac", "As", "Kh");

        Assert.That(straightFlush.CompareTo(fourOfAKind), Is.GreaterThan(0));
    }

    [Test]
    public void Test_Flushes_ShouldCompareByHighestCard()
    {
        var higher = Evaluate("Ah", "Jh", "9h", "5h", "2h");
        var lower = Evaluate("Kd", "Jd", "9d", "5d", "2d");

        Assert.That(higher.CompareTo(lower), Is.GreaterThan(0));
    }

    [Test]
    public void Test_AceHighStraight_ShouldBeatWheel()
    {
        var aceHigh = Evaluate("Ah", "Kd", "Qc", "Js", "Th");
        var wheel = Evaluate("Ad", "5d", "4c", "3s", "2h");

        Assert.That(aceHigh.CompareTo(wheel), Is.GreaterThan(0));
    }

    [Test]
    public void Test_EqualHands_ShouldCompareEqual()
    {
        var first = Evaluate("Ah", "Ad", "7c", "7s", "Kh");
        var second = Evaluate("As", "Ac", "7h", "7d", "Ks");

        Assert.That(first.CompareTo(second), Is.EqualTo(0));
    }

    [Test]
    public void Test_EvaluateBest_ShouldPickBestFiveOfSeven()
    {
        // Seven cards containing a flush in hearts plus noise.
        var cards = BEST_OF_SEVEN_CARDS.Select(Card).ToList();

        var evaluation = PokerHandEvaluator.EvaluateBest(cards);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.Flush));
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(BEST_FLUSH_TIEBREAKERS));
        }
    }

    [Test]
    public void Test_EvaluateBest_ShouldFindStraightUsingBothHoleCards()
    {
        // Board 5-6-7 + 2 + K, hole cards 8 and 9 -> straight 5..9.
        var cards = STRAIGHT_OF_SEVEN_CARDS.Select(Card).ToList();

        var evaluation = PokerHandEvaluator.EvaluateBest(cards);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evaluation.Rank, Is.EqualTo(PokerHandRank.Straight));
            Assert.That(evaluation.Tiebreakers, Is.EqualTo(BEST_STRAIGHT_TIEBREAKERS));
        }
    }
}
