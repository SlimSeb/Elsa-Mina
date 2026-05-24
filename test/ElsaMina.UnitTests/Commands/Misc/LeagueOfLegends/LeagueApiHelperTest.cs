using ElsaMina.Commands.Misc.LeagueOfLegends;

namespace ElsaMina.UnitTests.Commands.Misc.LeagueOfLegends;

[TestFixture]
public class LeagueApiHelperTest
{
    // --- TryParseInput ---

    [Test]
    public void Test_TryParseInput_ShouldReturnNull_WhenTargetIsEmpty()
    {
        Assert.That(LeagueApiHelper.TryParseInput(string.Empty), Is.Null);
    }

    [Test]
    public void Test_TryParseInput_ShouldReturnNull_WhenTargetHasNoHash()
    {
        Assert.That(LeagueApiHelper.TryParseInput("PlayerWithoutHash"), Is.Null);
    }

    [Test]
    public void Test_TryParseInput_ShouldReturnRiotIdWithDefaultPlatform_WhenNoRegionProvided()
    {
        var result = LeagueApiHelper.TryParseInput("Player#EUW");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.RiotId, Is.EqualTo("Player#EUW"));
            Assert.That(result.Value.Platform, Is.EqualTo("euw1"));
        }
    }

    [Test]
    public void Test_TryParseInput_ShouldReturnSpecifiedPlatform_WhenRegionProvided()
    {
        var result = LeagueApiHelper.TryParseInput("Player#NA, na1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.RiotId, Is.EqualTo("Player#NA"));
            Assert.That(result.Value.Platform, Is.EqualTo("na1"));
        }
    }

    [Test]
    public void Test_TryParseInput_ShouldTrimWhitespace_WhenInputHasSpaces()
    {
        var result = LeagueApiHelper.TryParseInput("  Player#EUW  ,  kr  ");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Value.RiotId, Is.EqualTo("Player#EUW"));
            Assert.That(result.Value.Platform, Is.EqualTo("kr"));
        }
    }

    // --- GetRouting ---

    [TestCase("euw1", "europe")]
    [TestCase("euw", "europe")]
    [TestCase("eun1", "europe")]
    [TestCase("eune", "europe")]
    [TestCase("tr1", "europe")]
    [TestCase("ru", "europe")]
    [TestCase("na1", "americas")]
    [TestCase("na", "americas")]
    [TestCase("br1", "americas")]
    [TestCase("la1", "americas")]
    [TestCase("la2", "americas")]
    [TestCase("kr", "asia")]
    [TestCase("jp1", "asia")]
    [TestCase("oc1", "sea")]
    [TestCase("oce", "sea")]
    public void Test_GetRouting_ShouldReturnCorrectRouting_ForKnownPlatform(string platform, string expectedRouting)
    {
        Assert.That(LeagueApiHelper.GetRouting(platform), Is.EqualTo(expectedRouting));
    }

    [Test]
    public void Test_GetRouting_ShouldBeCaseInsensitive()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(LeagueApiHelper.GetRouting("EUW1"), Is.EqualTo("europe"));
            Assert.That(LeagueApiHelper.GetRouting("NA1"), Is.EqualTo("americas"));
        }
    }

    [Test]
    public void Test_GetRouting_ShouldReturnNull_ForUnknownPlatform()
    {
        Assert.That(LeagueApiHelper.GetRouting("invalid"), Is.Null);
    }

    // --- SplitRiotId ---

    [Test]
    public void Test_SplitRiotId_ShouldReturnCorrectGameNameAndTagLine()
    {
        var (gameName, tagLine) = LeagueApiHelper.SplitRiotId("Player#EUW");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gameName, Is.EqualTo("Player"));
            Assert.That(tagLine, Is.EqualTo("EUW"));
        }
    }

    [Test]
    public void Test_SplitRiotId_ShouldHandleGameNameWithSpaces()
    {
        var (gameName, tagLine) = LeagueApiHelper.SplitRiotId("Cool Player#NA1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(gameName, Is.EqualTo("Cool Player"));
            Assert.That(tagLine, Is.EqualTo("NA1"));
        }
    }

    // --- BuildHeaders ---

    [Test]
    public void Test_BuildHeaders_ShouldReturnDictionaryWithRiotToken()
    {
        var headers = LeagueApiHelper.BuildHeaders("my-api-key");

        Assert.That(headers["X-Riot-Token"], Is.EqualTo("my-api-key"));
    }
}
