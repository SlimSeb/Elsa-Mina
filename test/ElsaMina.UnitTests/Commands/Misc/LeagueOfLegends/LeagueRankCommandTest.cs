using ElsaMina.Commands.Misc.LeagueOfLegends;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.LeagueOfLegends;

[TestFixture]
public class LeagueRankCommandTest
{
    private IHttpService _httpService;
    private IConfiguration _configuration;
    private LeagueRankCommand _command;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _configuration.RiotApiKey.Returns("test-api-key");

        _command = new LeagueRankCommand(_httpService, _configuration);
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
    public async Task Test_RunAsync_ShouldCallGetStringForSoloAndFlex_WhenBothQueuesPresent()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse([
            new LeagueEntryDto { QueueType = "RANKED_SOLO_5x5", Tier = "GOLD", Rank = "II", LeaguePoints = 50, Wins = 60, Losses = 40 },
            new LeagueEntryDto { QueueType = "RANKED_FLEX_SR", Tier = "SILVER", Rank = "I", LeaguePoints = 80, Wins = 30, Losses = 20 }
        ]);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).GetString("lolrank_solo", Arg.Any<object[]>());
        context.Received(1).GetString("lolrank_flex", Arg.Any<object[]>());
        context.Received(1).Reply(Arg.Any<string>(), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallGetStringForSoloOnly_WhenOnlySoloQueuePresent()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse([
            new LeagueEntryDto { QueueType = "RANKED_SOLO_5x5", Tier = "PLATINUM", Rank = "IV", LeaguePoints = 10, Wins = 100, Losses = 80 }
        ]);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).GetString("lolrank_solo", Arg.Any<object[]>());
        context.DidNotReceive().GetString("lolrank_flex", Arg.Any<object[]>());
        context.Received(1).Reply(Arg.Any<string>(), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallGetStringForFlexOnly_WhenOnlyFlexQueuePresent()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse([
            new LeagueEntryDto { QueueType = "RANKED_FLEX_SR", Tier = "DIAMOND", Rank = "III", LeaguePoints = 25, Wins = 50, Losses = 45 }
        ]);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.DidNotReceive().GetString("lolrank_solo", Arg.Any<object[]>());
        context.Received(1).GetString("lolrank_flex", Arg.Any<object[]>());
        context.Received(1).Reply(Arg.Any<string>(), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallGetStringForUnrankedQueues_WhenEntriesContainNoSoloOrFlex()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse([
            new LeagueEntryDto { QueueType = "RANKED_TFT", Tier = "GOLD", Rank = "I", LeaguePoints = 0, Wins = 10, Losses = 5 }
        ]);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).GetString("lolrank_unranked_queues");
        context.Received(1).Reply(Arg.Any<string>(), rankAware: true);
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

        await _command.RunAsync(context);

        context.Received(1).GetString("lolrank_solo",
            Arg.Is<object[]>(args => (int)args[5] == 75));
    }

    [Test]
    public async Task Test_RunAsync_ShouldComputeZeroWinRate_WhenEntryHasNoGames()
    {
        SetupAccountResponse("test-puuid");
        SetupEntriesResponse([
            new LeagueEntryDto { QueueType = "RANKED_SOLO_5x5", Tier = "GOLD", Rank = "II", LeaguePoints = 50, Wins = 0, Losses = 0 }
        ]);
        var context = MakeContext("Player#EUW");

        await _command.RunAsync(context);

        context.Received(1).GetString("lolrank_solo",
            Arg.Is<object[]>(args => (int)args[5] == 0));
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
