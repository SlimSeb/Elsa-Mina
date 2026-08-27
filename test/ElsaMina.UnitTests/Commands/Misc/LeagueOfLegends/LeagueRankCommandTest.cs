using ElsaMina.Commands.Misc.LeagueOfLegends;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.LeagueOfLegends;

[TestFixture]
public class LeagueRankCommandTest
{
    private IHttpService _httpService;
    private IConfiguration _configuration;
    private ITemplatesManager _templatesManager;
    private LeagueRankCommand _command;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration.RiotApiKey.Returns("test-api-key");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<html/>");

        _command = new LeagueRankCommand(_httpService, _configuration, _templatesManager);
    }

    private IContext MakeContext(string target)
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns(target);
        return context;
    }

    private void SetupAccountResponse(string puuid)
    {
        _httpService
            .SendAsync<RiotAccountDto>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<RiotAccountDto> { Data = new RiotAccountDto { Puuid = puuid } });
    }

    private void SetupEntriesResponse(List<LeagueEntryDto> entries)
    {
        _httpService
            .SendAsync<List<LeagueEntryDto>>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<List<LeagueEntryDto>> { Data = entries });
    }

    // --- Properties ---

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldReturnTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    // --- Input validation ---

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNoApiKey_WhenApiKeyIsEmpty()
    {
        _configuration.RiotApiKey.Returns(string.Empty);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolrank_no_api_key");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelp_WhenTargetIsEmpty()
    {
        var context = MakeContext(string.Empty);

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolrank_help");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelp_WhenTargetHasNoHash()
    {
        var context = MakeContext("PlayerWithoutHash");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolrank_help");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithInvalidRegion_WhenRegionIsUnknown()
    {
        var context = MakeContext("Player#EUW, invalid-region");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolrank_invalid_region", "invalid-region");
    }

    // --- Account API ---

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithPlayerNotFound_WhenAccountApiReturnsNullPuuid()
    {
        _httpService
            .SendAsync<RiotAccountDto>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<RiotAccountDto> { Data = new RiotAccountDto { Puuid = null } });
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolrank_player_not_found", "Player#EUW");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallAccountApiWithCorrectRoutingRegion_WhenRegionIsNa1()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse([]);
        var context = MakeContext("Player#NA1, na1");

        await _command.RunAsync(context);

        await _httpService.Received(1).SendAsync<RiotAccountDto>(
            Arg.Is<HttpRequest>(request => request.Uri.Contains("americas.api.riotgames.com")),
            Arg.Any<CancellationToken>());
    }

    // --- Entries ---

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithUnranked_WhenEntriesListIsEmpty()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse([]);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolrank_unranked", "Player", "EUW");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithUnranked_WhenEntriesListIsNull()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse(null);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolrank_unranked", "Player", "EUW");
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplateAndReplyHtml_WhenBothQueuesPresent()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse([
            new LeagueEntryDto { QueueType = "RANKED_SOLO_5x5", Tier = "GOLD", Rank = "II", LeaguePoints = 50, Wins = 60, Losses = 40 },
            new LeagueEntryDto { QueueType = "RANKED_FLEX_SR", Tier = "SILVER", Rank = "I", LeaguePoints = 80, Wins = 30, Losses = 20 }
        ]);
        var context = MakeContext("Player#EUW");

        LeagueRankViewModel capturedVm = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<LeagueRankViewModel>(vm => capturedVm = vm));

        await _command.RunAsync(context);

        await _templatesManager.Received(1).GetTemplateAsync("Misc/LeagueOfLegends/LeagueRank", Arg.Any<LeagueRankViewModel>());
        context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedVm, Is.Not.Null);
            Assert.That(capturedVm.GameName, Is.EqualTo("Player"));
            Assert.That(capturedVm.TagLine, Is.EqualTo("EUW"));
            Assert.That(capturedVm.SoloQueue.IsUnranked, Is.False);
            Assert.That(capturedVm.SoloQueue.Tier, Is.EqualTo("GOLD"));
            Assert.That(capturedVm.SoloQueue.FormattedRank, Is.EqualTo("GOLD II"));
            Assert.That(capturedVm.SoloQueue.LeaguePoints, Is.EqualTo(50));
            Assert.That(capturedVm.SoloQueue.Wins, Is.EqualTo(60));
            Assert.That(capturedVm.SoloQueue.Losses, Is.EqualTo(40));
            Assert.That(capturedVm.SoloQueue.WinRate, Is.EqualTo(60));
            Assert.That(capturedVm.SoloQueue.EmblemUrl, Does.Contain("emblem-gold.png"));
            Assert.That(capturedVm.FlexQueue.IsUnranked, Is.False);
            Assert.That(capturedVm.FlexQueue.Tier, Is.EqualTo("SILVER"));
            Assert.That(capturedVm.FlexQueue.FormattedRank, Is.EqualTo("SILVER I"));
            Assert.That(capturedVm.FlexQueue.LeaguePoints, Is.EqualTo(80));
            Assert.That(capturedVm.FlexQueue.WinRate, Is.EqualTo(60));
            Assert.That(capturedVm.FlexQueue.EmblemUrl, Does.Contain("emblem-silver.png"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldMarkFlexAsUnranked_WhenOnlySoloQueuePresent()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse([
            new LeagueEntryDto { QueueType = "RANKED_SOLO_5x5", Tier = "PLATINUM", Rank = "IV", LeaguePoints = 10, Wins = 100, Losses = 80 }
        ]);
        var context = MakeContext("Player#EUW");

        LeagueRankViewModel capturedVm = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<LeagueRankViewModel>(vm => capturedVm = vm));

        await _command.RunAsync(context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedVm, Is.Not.Null);
            Assert.That(capturedVm.SoloQueue.IsUnranked, Is.False);
            Assert.That(capturedVm.SoloQueue.Tier, Is.EqualTo("PLATINUM"));
            Assert.That(capturedVm.FlexQueue.IsUnranked, Is.True);
            Assert.That(capturedVm.FlexQueue.EmblemUrl, Does.Contain("unranked.png"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldMarkSoloAsUnranked_WhenOnlyFlexQueuePresent()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse([
            new LeagueEntryDto { QueueType = "RANKED_FLEX_SR", Tier = "DIAMOND", Rank = "III", LeaguePoints = 25, Wins = 50, Losses = 45 }
        ]);
        var context = MakeContext("Player#EUW");

        LeagueRankViewModel capturedVm = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<LeagueRankViewModel>(vm => capturedVm = vm));

        await _command.RunAsync(context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedVm, Is.Not.Null);
            Assert.That(capturedVm.SoloQueue.IsUnranked, Is.True);
            Assert.That(capturedVm.SoloQueue.EmblemUrl, Does.Contain("unranked.png"));
            Assert.That(capturedVm.FlexQueue.IsUnranked, Is.False);
            Assert.That(capturedVm.FlexQueue.Tier, Is.EqualTo("DIAMOND"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldComputeWinRateCorrectly_WhenEntryHasWinsAndLosses()
    {
        SetupAccountResponse("test-puuid");
        // 3W / (3+1) = 75%
        SetupEntriesResponse([
            new LeagueEntryDto { QueueType = "RANKED_SOLO_5x5", Tier = "GOLD", Rank = "II", LeaguePoints = 50, Wins = 3, Losses = 1 }
        ]);
        var context = MakeContext("Player#EUW");

        LeagueRankViewModel capturedVm = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<LeagueRankViewModel>(vm => capturedVm = vm));

        await _command.RunAsync(context);

        Assert.That(capturedVm.SoloQueue.WinRate, Is.EqualTo(75));
    }

    [Test]
    public async Task Test_RunAsync_ShouldComputeZeroWinRate_WhenEntryHasNoGames()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse([
            new LeagueEntryDto { QueueType = "RANKED_SOLO_5x5", Tier = "GOLD", Rank = "II", LeaguePoints = 50, Wins = 0, Losses = 0 }
        ]);
        var context = MakeContext("Player#EUW");

        LeagueRankViewModel capturedVm = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<LeagueRankViewModel>(vm => capturedVm = vm));

        await _command.RunAsync(context);

        Assert.That(capturedVm.SoloQueue.WinRate, Is.EqualTo(0));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseDefaultPlatform_WhenNoRegionProvided()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse([]);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        await _httpService.Received(1).SendAsync<List<LeagueEntryDto>>(
            Arg.Is<HttpRequest>(request => request.Uri.Contains("euw1.api.riotgames.com")),
            Arg.Any<CancellationToken>());
    }

    // --- Error handling ---

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithError_WhenHttpThrows()
    {
        _httpService
            .SendAsync<RiotAccountDto>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("network failure"));
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolrank_error");
    }
}
