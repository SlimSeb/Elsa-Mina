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

    // --- GetRankEmblemUrl ---

    [TestCase("GOLD", "https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-static-assets/global/default/images/ranked-emblem/emblem-gold.png")]
    [TestCase("diamond", "https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-static-assets/global/default/images/ranked-emblem/emblem-diamond.png")]
    [TestCase("CHALLENGER", "https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-static-assets/global/default/images/ranked-emblem/emblem-challenger.png")]
    public void Test_GetRankEmblemUrl_ShouldReturnCorrectUrl_ForTier(string tier, string expectedUrl)
    {
        Assert.That(LeagueApiHelper.GetRankEmblemUrl(tier), Is.EqualTo(expectedUrl));
    }

    [Test]
    public void Test_GetRankEmblemUrl_ShouldReturnUnrankedUrl_WhenTierIsNullOrEmpty()
    {
        Assert.That(LeagueApiHelper.GetRankEmblemUrl(null),
            Is.EqualTo("https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-static-assets/global/default/images/ranked-mini-crests/unranked.png"));
        Assert.That(LeagueApiHelper.GetRankEmblemUrl(""),
            Is.EqualTo("https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-static-assets/global/default/images/ranked-mini-crests/unranked.png"));
    }

    // --- GetChampionIconUrl ---

    [Test]
    public void Test_GetChampionIconUrl_ShouldReturnCommunityDragonUrl_WhenChampionIdIsPositive()
    {
        Assert.That(LeagueApiHelper.GetChampionIconUrl(222, "Jinx"),
            Is.EqualTo("https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/champion-icons/222.png"));
    }

    [Test]
    public void Test_GetChampionIconUrl_ShouldReturnDDragonUrl_WhenChampionIdIsZeroAndNameProvided()
    {
        Assert.That(LeagueApiHelper.GetChampionIconUrl(0, "Ahri"),
            Is.EqualTo("https://ddragon.leagueoflegends.com/cdn/14.24.1/img/champion/Ahri.png"));
    }

    [Test]
    public void Test_GetChampionIconUrl_ShouldReturnFallbackUrl_WhenNoIdOrNameProvided()
    {
        Assert.That(LeagueApiHelper.GetChampionIconUrl(0, null),
            Is.EqualTo("https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/champion-icons/-1.png"));
    }

    // --- GetTierColor ---

    [TestCase("GOLD", "#eec152")]
    [TestCase("diamond", "#6ba6ff")]
    [TestCase("CHALLENGER", "#fde047")]
    [TestCase("iron", "#9e948d")]
    [TestCase(null, "#8a96a3")]
    public void Test_GetTierColor_ShouldReturnExpectedHexCode(string tier, string expectedHex)
    {
        Assert.That(LeagueApiHelper.GetTierColor(tier), Is.EqualTo(expectedHex));
    }

    // --- FormatTierRank ---

    [TestCase("GOLD", "II", "GOLD II")]
    [TestCase("diamond", "iv", "DIAMOND IV")]
    [TestCase("CHALLENGER", "I", "CHALLENGER")]
    [TestCase("MASTER", "I", "MASTER")]
    [TestCase("GRANDMASTER", "I", "GRANDMASTER")]
    [TestCase("SILVER", null, "SILVER")]
    [TestCase(null, null, "Unranked")]
    public void Test_FormatTierRank_ShouldFormatCorrectly(string tier, string rank, string expected)
    {
        Assert.That(LeagueApiHelper.FormatTierRank(tier, rank), Is.EqualTo(expected));
    }

    // --- CalculateKdaRatio ---

    [Test]
    public void Test_CalculateKdaRatio_ShouldReturnPerfect_WhenDeathsIsZero()
    {
        Assert.That(LeagueApiHelper.CalculateKdaRatio(5, 0, 10), Is.EqualTo("Perfect"));
    }

    [Test]
    public void Test_CalculateKdaRatio_ShouldComputeCorrectRatio_WhenDeathsIsPositive()
    {
        Assert.That(LeagueApiHelper.CalculateKdaRatio(5, 2, 8), Is.EqualTo("6.50"));
    }

    // --- CalculateCsPerMinute ---

    [Test]
    public void Test_CalculateCsPerMinute_ShouldComputeCorrectCsPerMinute()
    {
        Assert.That(LeagueApiHelper.CalculateCsPerMinute(200, 25), Is.EqualTo("8.0"));
    }

    [Test]
    public void Test_CalculateCsPerMinute_ShouldReturnZero_WhenDurationIsZero()
    {
        Assert.That(LeagueApiHelper.CalculateCsPerMinute(100, 0), Is.EqualTo("0.0"));
    }
}
