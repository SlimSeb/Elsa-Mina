using ElsaMina.Commands.Games.Tcg.Cards;

namespace ElsaMina.UnitTests.Commands.Games.Tcg;

public class TcgCardPoolTest
{
    [Test]
    public void Test_AllCards_ShouldNotBeEmpty()
    {
        Assert.That(TcgCardPool.AllCards, Is.Not.Empty);
    }

    [Test]
    public void Test_AllCards_ShouldHaveUniqueIds()
    {
        var ids = TcgCardPool.AllCards.Select(card => card.Id).ToList();
        Assert.That(ids, Is.Unique);
    }

    [Test]
    public void Test_AllCards_ShouldHaveLowercaseSlugSpecies()
    {
        Assert.That(TcgCardPool.AllCards.All(card => card.Species == card.Species.ToLowerInvariant()), Is.True);
    }

    [Test]
    public void Test_SpriteUrl_ShouldBeBuiltFromSpecies()
    {
        var card = TcgCardPool.AllCards.First();
        Assert.That(card.SpriteUrl,
            Is.EqualTo($"https://play.pokemonshowdown.com/sprites/gen5ani/{card.Species}.gif"));
    }

    [Test]
    public void Test_AllCards_ShouldHaveSaneStats()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TcgCardPool.AllCards.All(card => card.Hp is >= 30 and <= 250), Is.True);
            Assert.That(TcgCardPool.AllCards.All(card => card.RetreatCost is >= 0 and <= 4), Is.True);
            Assert.That(TcgCardPool.AllCards.All(card => card.Attacks.Count > 0), Is.True);
            Assert.That(TcgCardPool.AllCards.SelectMany(card => card.Attacks)
                .All(attack => attack.Damage >= 0 && attack.Cost.Count > 0), Is.True);
        }
    }

    [Test]
    public void Test_TryGet_ShouldReturnCard_WhenIdExists()
    {
        var anyId = TcgCardPool.AllCards.First().Id;

        var found = TcgCardPool.TryGet(anyId, out var card);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.True);
            Assert.That(card.Id, Is.EqualTo(anyId));
        }
    }

    [Test]
    public void Test_TryGet_ShouldBeCaseInsensitiveAndTrimmed()
    {
        var anyId = TcgCardPool.AllCards.First().Id;

        var found = TcgCardPool.TryGet($"  {anyId.ToUpperInvariant()} ", out var card);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found, Is.True);
            Assert.That(card.Id, Is.EqualTo(anyId));
        }
    }

    [Test]
    public void Test_TryGet_ShouldReturnFalse_WhenIdUnknownOrBlank()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TcgCardPool.TryGet("not-a-real-card", out _), Is.False);
            Assert.That(TcgCardPool.TryGet("", out _), Is.False);
            Assert.That(TcgCardPool.TryGet(null, out _), Is.False);
        }
    }

    [Test]
    public void Test_ByType_ShouldReturnOnlyMatchingCards()
    {
        var fireCards = TcgCardPool.ByType(TcgType.Fire).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fireCards, Is.Not.Empty);
            Assert.That(fireCards.All(card => card.Type == TcgType.Fire), Is.True);
        }
    }
}
