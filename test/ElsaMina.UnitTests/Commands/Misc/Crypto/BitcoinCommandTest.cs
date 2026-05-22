using ElsaMina.Commands.Misc.Crypto;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.Crypto;

[TestFixture]
public class BitcoinCommandTests
{
    private IHttpService _httpService;
    private BitcoinCommand _bitcoinCommand;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _context = Substitute.For<IContext>();
        _bitcoinCommand = new BitcoinCommand(_httpService);
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldReturnTrue()
    {
        Assert.That(_bitcoinCommand.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_bitcoinCommand.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithBitcoinRates_WhenApiCallSucceeds()
    {
        // Arrange
        var mockResponse = new HttpResponse<IDictionary<string, IDictionary<string, double>>>
        {
            Data = new Dictionary<string, IDictionary<string, double>>
            {
                ["bitcoin"] = new Dictionary<string, double>
                {
                    ["eur"] = 40000,
                    ["usd"] = 42000
                }
            }
        };
        _httpService.GetAsync<IDictionary<string, IDictionary<string, double>>>(Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(mockResponse);

        // Act
        await _bitcoinCommand.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("1 bitcoin = 40000€ = 42000$", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithError_WhenApiCallFails()
    {
        // Arrange
        _httpService.GetAsync<IDictionary<string, IDictionary<string, double>>>(Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Throws(new Exception("API error"));

        // Act
        await _bitcoinCommand.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("bitcoin_error");
    }
}
