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
public class LeagueOfLegendsHistoryCommandTest
{
    private IHttpService _httpService;
    private IConfiguration _configuration;
    private ITemplatesManager _templatesManager;
    private LeagueOfLegendsHistoryCommand _command;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _configuration.RiotApiKey.Returns("test-api-key");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<html/>");

        _command = new LeagueOfLegendsHistoryCommand(_httpService, _configuration, _templatesManager);
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

    private void SetupMatchIdsResponse(List<string> matchIds)
    {
        _httpService
            .SendAsync<List<string>>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<List<string>> { Data = matchIds });
    }

    private void SetupMatchResponse(string puuid, bool win = true, string championName = "Jinx",
        int kills = 5, int deaths = 2, int assists = 8, int queueId = 420, int gameDuration = 1500, int championId = 222,
        long gameCreation = 1700000000000, long gameEndTimestamp = 1700001500000)
    {
        _httpService
            .SendAsync<MatchDto>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<MatchDto>
            {
                Data = new MatchDto
                {
                    Info = new MatchInfoDto
                    {
                        QueueId = queueId,
                        GameDuration = gameDuration,
                        GameCreation = gameCreation,
                        GameEndTimestamp = gameEndTimestamp,
                        Participants =
                        [
                            new MatchParticipantDto
                            {
                                Puuid = puuid,
                                ChampionId = championId,
                                ChampionName = championName,
                                Kills = kills,
                                Deaths = deaths,
                                Assists = assists,
                                Win = win,
                                TotalMinionsKilled = 150,
                                NeutralMinionsKilled = 20
                            }
                        ]
                    }
                }
            });
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

        context.Received(1).ReplyLocalizedMessage("lolhistory_no_api_key");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelp_WhenTargetIsEmpty()
    {
        var context = MakeContext(string.Empty);

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolhistory_help");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithHelp_WhenTargetHasNoHash()
    {
        var context = MakeContext("PlayerWithoutHash");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolhistory_help");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithInvalidRegion_WhenRegionIsUnknown()
    {
        var context = MakeContext("Player#EUW, badregion");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolhistory_invalid_region", "badregion");
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

        context.Received(1).ReplyLocalizedMessage("lolhistory_player_not_found", "Player#EUW");
    }

    // --- Match IDs ---

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNoGames_WhenMatchIdsListIsEmpty()
    {
        SetupAccountResponse("test-puuid");
        SetupMatchIdsResponse([]);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolhistory_no_games", "Player", "EUW");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNoGames_WhenMatchIdsListIsNull()
    {
        SetupAccountResponse("test-puuid");
        SetupMatchIdsResponse(null);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolhistory_no_games", "Player", "EUW");
    }

    [Test]
    public async Task Test_RunAsync_ShouldRequestMatchIdsWithCorrectRoutingUrl_WhenRegionIsKr()
    {
        SetupAccountResponse("test-puuid");
        SetupMatchIdsResponse([]);
        var context = MakeContext("Player#KR, kr");

        await _command.RunAsync(context);

        await _httpService.Received(1).SendAsync<List<string>>(
            Arg.Is<HttpRequest>(request => request.Uri.Contains("asia.api.riotgames.com")),
            Arg.Any<CancellationToken>());
    }

    // --- Match details ---

    [Test]
    public async Task Test_RunAsync_ShouldFetchOneMatchPerMatchId()
    {
        const string puuid = "test-puuid";
        SetupAccountResponse(puuid);
        SetupMatchIdsResponse(["EUW1_001", "EUW1_002", "EUW1_003"]);
        SetupMatchResponse(puuid);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        await _httpService.Received(3).SendAsync<MatchDto>(
            Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplateAndReplyHtml_WhenMatchesAreReturned()
    {
        const string puuid = "test-puuid";
        SetupAccountResponse(puuid);
        SetupMatchIdsResponse(["EUW1_001"]);
        SetupMatchResponse(puuid, win: true, championName: "Jinx", kills: 5, deaths: 2, assists: 8, championId: 222);
        var context = MakeContext("Player#EUW");

        LeagueHistoryViewModel capturedVm = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<LeagueHistoryViewModel>(vm => capturedVm = vm));

        await _command.RunAsync(context);

        await _templatesManager.Received(1).GetTemplateAsync("Misc/LeagueOfLegends/LeagueHistory", Arg.Any<LeagueHistoryViewModel>());
        context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedVm, Is.Not.Null);
            Assert.That(capturedVm.GameName, Is.EqualTo("Player"));
            Assert.That(capturedVm.TagLine, Is.EqualTo("EUW"));
            Assert.That(capturedVm.Games.Count, Is.EqualTo(1));
            Assert.That(capturedVm.Games[0].ChampionName, Is.EqualTo("Jinx"));
            Assert.That(capturedVm.Games[0].ChampionId, Is.EqualTo(222));
            Assert.That(capturedVm.Games[0].ChampionIconUrl, Does.Contain("222.png"));
            Assert.That(capturedVm.Games[0].Win, Is.True);
            Assert.That(capturedVm.Games[0].Kills, Is.EqualTo(5));
            Assert.That(capturedVm.Games[0].Deaths, Is.EqualTo(2));
            Assert.That(capturedVm.Games[0].Assists, Is.EqualTo(8));
            Assert.That(capturedVm.Games[0].KdaRatio, Is.EqualTo("6.50"));
            Assert.That(capturedVm.Games[0].Cs, Is.EqualTo(170));
            Assert.That(capturedVm.Games[0].DurationMinutes, Is.EqualTo(25));
            Assert.That(capturedVm.Games[0].FormattedDate, Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetWinStatusCorrectly_WhenParticipantLost()
    {
        const string puuid = "test-puuid";
        SetupAccountResponse(puuid);
        SetupMatchIdsResponse(["EUW1_001"]);
        SetupMatchResponse(puuid, win: false);
        var context = MakeContext("Player#EUW");

        LeagueHistoryViewModel capturedVm = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<LeagueHistoryViewModel>(vm => capturedVm = vm));

        await _command.RunAsync(context);

        Assert.That(capturedVm.Games[0].Win, Is.False);
    }

    [Test]
    public async Task Test_RunAsync_ShouldComputeCsAsSumOfMinionsAndNeutral()
    {
        const string puuid = "test-puuid";
        SetupAccountResponse(puuid);
        SetupMatchIdsResponse(["EUW1_001"]);
        _httpService
            .SendAsync<MatchDto>(Arg.Any<HttpRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<MatchDto>
            {
                Data = new MatchDto
                {
                    Info = new MatchInfoDto
                    {
                        QueueId = 420,
                        GameDuration = 1200,
                        Participants =
                        [
                            new MatchParticipantDto
                            {
                                Puuid = puuid,
                                ChampionName = "Ahri",
                                ChampionId = 103,
                                TotalMinionsKilled = 180,
                                NeutralMinionsKilled = 20,
                                Win = true
                            }
                        ]
                    }
                }
            });
        var context = MakeContext("Player#EUW");

        LeagueHistoryViewModel capturedVm = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<LeagueHistoryViewModel>(vm => capturedVm = vm));

        await _command.RunAsync(context);

        // CS = 180 + 20 = 200
        Assert.That(capturedVm.Games[0].Cs, Is.EqualTo(200));
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassDurationInMinutes()
    {
        const string puuid = "test-puuid";
        SetupAccountResponse(puuid);
        SetupMatchIdsResponse(["EUW1_001"]);
        SetupMatchResponse(puuid, gameDuration: 1800); // 30 minutes
        var context = MakeContext("Player#EUW");

        LeagueHistoryViewModel capturedVm = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<LeagueHistoryViewModel>(vm => capturedVm = vm));

        await _command.RunAsync(context);

        Assert.That(capturedVm.Games[0].DurationMinutes, Is.EqualTo(30));
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

        context.Received(1).ReplyLocalizedMessage("lolhistory_error");
    }
}
