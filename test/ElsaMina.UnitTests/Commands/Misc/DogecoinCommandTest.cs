using ElsaMina.Commands.Misc;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc;

[TestFixture]
public class DogecoinCommandTest
{
    private IHttpService _httpService;
    private DogecoinCommand _command;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _context = Substitute.For<IContext>();
        _command = new DogecoinCommand(_httpService);
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
    public async Task Test_RunAsync_ShouldReplyWithDogecoinRates_WhenApiCallSucceeds()
    {
        // Arrange
        var mockResponse = new HttpResponse<IDictionary<string, IDictionary<string, double>>>
        {
            Data = new Dictionary<string, IDictionary<string, double>>
            {
                ["dogecoin"] = new Dictionary<string, double>
                {
                    ["eur"] = 0.0851,
                    ["usd"] = 0.0923
                }
            }
        };
        _httpService.GetAsync<IDictionary<string, IDictionary<string, double>>>(Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(mockResponse);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply("1 Dogecoin = 0.0851€ = 0.0923$", rankAware: true);
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
        _context.Received(1).ReplyLocalizedMessage("dogecoin_error");
    }
}
