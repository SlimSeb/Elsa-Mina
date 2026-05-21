using ElsaMina.Commands.Misc.LeagueOfLegends;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.LeagueOfLegends;

[TestFixture]
public class LeagueOfLegendsHistoryCommandTest
{
    private IHttpService _httpService;
    private IConfiguration _configuration;
    private LeagueOfLegendsHistoryCommand _command;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _configuration.RiotApiKey.Returns("test-api-key");

        _command = new LeagueOfLegendsHistoryCommand(_httpService, _configuration);
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
            .GetAsync<RiotAccountDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                Arg.Any<IDictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<RiotAccountDto> { Data = new RiotAccountDto { Puuid = puuid } });
    }

    private void SetupMatchIdsResponse(List<string> matchIds)
    {
        _httpService
            .GetAsync<List<string>>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                Arg.Any<IDictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<List<string>> { Data = matchIds });
    }

    private void SetupMatchResponse(string puuid, bool win = true, string championName = "Jinx",
        int kills = 5, int deaths = 2, int assists = 8, int queueId = 420, int gameDuration = 1500)
    {
        _httpService
            .GetAsync<MatchDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                Arg.Any<IDictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponse<MatchDto>
            {
                Data = new MatchDto
                {
                    Info = new MatchInfoDto
                    {
                        QueueId = queueId,
                        GameDuration = gameDuration,
                        Participants =
                        [
                            new MatchParticipantDto
                            {
                                Puuid = puuid,
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
            .GetAsync<RiotAccountDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                Arg.Any<IDictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
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

        await _httpService.Received(1).GetAsync<List<string>>(
            Arg.Is<string>(url => url.Contains("asia.api.riotgames.com")),
            Arg.Any<IDictionary<string, string>>(), Arg.Any<IDictionary<string, string>>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
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

        await _httpService.Received(3).GetAsync<MatchDto>(
            Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<IDictionary<string, string>>(),
            Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReply_WhenMatchesAreReturned()
    {
        const string puuid = "test-puuid";
        SetupAccountResponse(puuid);
        SetupMatchIdsResponse(["EUW1_001"]);
        SetupMatchResponse(puuid);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).Reply(Arg.Any<string>(), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallGetStringForWin_WhenParticipantWon()
    {
        const string puuid = "test-puuid";
        SetupAccountResponse(puuid);
        SetupMatchIdsResponse(["EUW1_001"]);
        SetupMatchResponse(puuid, win: true);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).GetString("lolhistory_win");
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallGetStringForLoss_WhenParticipantLost()
    {
        const string puuid = "test-puuid";
        SetupAccountResponse(puuid);
        SetupMatchIdsResponse(["EUW1_001"]);
        SetupMatchResponse(puuid, win: false);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).GetString("lolhistory_loss");
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCorrectKdaToGameEntry()
    {
        const string puuid = "test-puuid";
        SetupAccountResponse(puuid);
        SetupMatchIdsResponse(["EUW1_001"]);
        SetupMatchResponse(puuid, kills: 10, deaths: 3, assists: 7);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).GetString("lolhistory_game_entry",
            Arg.Is<object[]>(args => (int)args[2] == 10 && (int)args[3] == 3 && (int)args[4] == 7));
    }

    [Test]
    public async Task Test_RunAsync_ShouldComputeCsAsSumOfMinionsAndNeutral()
    {
        const string puuid = "test-puuid";
        SetupAccountResponse(puuid);
        SetupMatchIdsResponse(["EUW1_001"]);
        _httpService
            .GetAsync<MatchDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                Arg.Any<IDictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
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
                                TotalMinionsKilled = 180,
                                NeutralMinionsKilled = 20,
                                Win = true
                            }
                        ]
                    }
                }
            });
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        // CS = 180 + 20 = 200
        context.Received(1).GetString("lolhistory_game_entry",
            Arg.Is<object[]>(args => (int)args[5] == 200));
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassDurationInMinutes()
    {
        const string puuid = "test-puuid";
        SetupAccountResponse(puuid);
        SetupMatchIdsResponse(["EUW1_001"]);
        SetupMatchResponse(puuid, gameDuration: 1800); // 30 minutes
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).GetString("lolhistory_game_entry",
            Arg.Is<object[]>(args => (int)args[7] == 30));
    }

    // --- Error handling ---

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithError_WhenHttpThrows()
    {
        _httpService
            .GetAsync<RiotAccountDto>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                Arg.Any<IDictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Throws(new Exception("network failure"));
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("lolhistory_error");
    }
}
