using ElsaMina.Commands.Misc.Crypto;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.Crypto;

[TestFixture]
public class EthereumCommandTest
{
    private IHttpService _httpService;
    private EthereumCommand _command;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _context = Substitute.For<IContext>();
        _command = new EthereumCommand(_httpService);
    }

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

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithEthereumRates_WhenApiCallSucceeds()
    {
        // Arrange
        var mockResponse = new HttpResponse<IDictionary<string, IDictionary<string, double>>>
        {
            Data = new Dictionary<string, IDictionary<string, double>>
            {
                ["ethereum"] = new Dictionary<string, double>
                {
                    ["eur"] = 2500.50,
                    ["usd"] = 2750.75
                }
            }
        };
        _httpService.GetAsync<IDictionary<string, IDictionary<string, double>>>(Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(mockResponse);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("1 Ethereum = 2500.50€ = 2750.75$", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithError_WhenApiCallFails()
    {
        // Arrange
        _httpService.GetAsync<IDictionary<string, IDictionary<string, double>>>(Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Throws(new Exception("API error"));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("ethereum_error");
    }
}
