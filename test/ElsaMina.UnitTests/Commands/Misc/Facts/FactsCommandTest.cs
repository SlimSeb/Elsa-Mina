using ElsaMina.Commands.Misc.Facts;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Http;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.Facts;

public class FactsCommandTest
{
    private IHttpService _httpService;
    private FactsCommand _factsCommand;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _context = Substitute.For<IContext>();

        _factsCommand = new FactsCommand(_httpService);
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldReturnTrue()
    {
        // Assert
        Assert.That(_factsCommand.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithFact_WhenApiCallSucceeds()
    {
        // Arrange
        var mockResponse = new HttpResponse<FactDto>
        {
            Data = new FactDto
            {
                Text = "Bananas are berries, but strawberries are not!"
            }
        };
        _httpService.SendAsync<FactDto>(Arg.Any<HttpRequest>()).Returns(mockResponse);
        _context.Command.Returns("fact");

        // Act
        await _factsCommand.RunAsync(_context);

        // Assert
        _context.Received().Reply("**Fact**: Bananas are berries, but strawberries are not!", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithGermanFact_WhenCommandIsFactDe()
    {
        // Arrange
        var mockResponse = new HttpResponse<FactDto>
        {
            Data = new FactDto
            {
                Text = "Die Banane ist eine Beere, die Erdbeere jedoch nicht."
            }
        };
        _httpService.SendAsync<FactDto>(Arg.Any<HttpRequest>()).Returns(mockResponse);
        _context.Command.Returns("factde");

        // Act
        await _factsCommand.RunAsync(_context);

        // Assert
        _context.Received().Reply("**Fact**: Die Banane ist eine Beere, die Erdbeere jedoch nicht.", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithError_WhenApiCallFails()
    {
        // Arrange
        _httpService.SendAsync<FactDto>(Arg.Any<HttpRequest>())
            .Throws(new Exception("API error"));

        // Act
        await _factsCommand.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("fact_error");
    }
}
