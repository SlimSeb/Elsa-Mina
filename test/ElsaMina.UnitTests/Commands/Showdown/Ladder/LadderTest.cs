using ElsaMina.Commands.Showdown.Ladder;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Formats;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Showdown.Ladder;

public class LadderCommandTest
{
    private LadderCommand _ladderCommand;
    private IHttpService _httpService;
    private ITemplatesManager _templatesManager;
    private IFormatsManager _formatsManager;
    private LadderHistoryManager _ladderHistoryManager;
    private IContext _context;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _formatsManager = Substitute.For<IFormatsManager>();
        _ladderHistoryManager = new LadderHistoryManager();
        _context = Substitute.For<IContext>();
        _ladderCommand = new LadderCommand(_httpService, _templatesManager, _formatsManager, _ladderHistoryManager);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNoPlayersMessage_WhenResponseIsNull()
    {
        // Arrange
        _httpService.SendAsync<LadderDto>(Arg.Any<HttpRequest>()).ReturnsNull();
        _context.Target.Returns("gen8ou");

        // Act
        await _ladderCommand.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("ladder_no_players");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNoPlayersMessage_WhenTopListIsNull()
    {
        // Arrange
        var httpResponse = new HttpResponse<LadderDto> { Data = new LadderDto { TopList = null, Format = "gen8ou" } };
        _httpService.SendAsync<LadderDto>(Arg.Any<HttpRequest>()).Returns(httpResponse);
        _context.Target.Returns("gen8ou");

        // Act
        await _ladderCommand.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("ladder_no_players");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithNoPlayersMessage_WhenNoPlayersMatchPrefix()
    {
        // 
        var httpResponse = new HttpResponse<LadderDto>
        {
            Data = new LadderDto
                { TopList = new List<LadderPlayerDto> { new() { Username = "player1" } }, Format = "gen8ou" }
        };
        _httpService.SendAsync<LadderDto>(Arg.Any<HttpRequest>()).Returns(httpResponse);
        _context.Target.Returns("gen8ou,xyz");

        // Act
        await _ladderCommand.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("ladder_no_players");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendHtml_WhenPlayersMatchPrefix()
    {
        // Arrange
        var player = new LadderPlayerDto { Username = "player1" };
        var httpResponse = new HttpResponse<LadderDto>
            { Data = new LadderDto { TopList = new List<LadderPlayerDto> { player }, Format = "gen8ou" } };
        _httpService.SendAsync<LadderDto>(Arg.Any<HttpRequest>()).Returns(httpResponse);
        _formatsManager.GetCleanFormat("gen8ou").Returns("[Gen 8] OU");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<LadderTableViewModel>())
            .Returns(Task.FromResult("formatted_html"));
        _context.Target.Returns("gen8ou,pla");

        // Act
        await _ladderCommand.RunAsync(_context);

        // Assert
        await _templatesManager.Received().GetTemplateAsync("Showdown/Ladder/LadderTable",
            Arg.Is<LadderTableViewModel>(vm => vm.Format == "[Gen 8] OU"));
        _context.Received().ReplyHtml("formatted_html", rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyWithErrorMessage_WhenExceptionOccurs()
    {
        // Arrange
        _httpService.SendAsync<LadderDto>(Arg.Any<HttpRequest>())
            .Throws(new Exception("API failure"));
        _context.Target.Returns("gen8ou");

        // Act
        await _ladderCommand.RunAsync(_context);

        // Assert
        _context.Received().ReplyLocalizedMessage("ladder_error");
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetEloTrend_WhenSameLadderIsRequestedTwice()
    {
        // Arrange
        var firstResponse = new HttpResponse<LadderDto>
        {
            Data = new LadderDto
            {
                Format = "gen8ou",
                TopList =
                [
                    new LadderPlayerDto { UserId = "player1", Username = "player1", Elo = 1500 },
                    new LadderPlayerDto { UserId = "player2", Username = "player2", Elo = 1600 }
                ]
            }
        };

        var secondResponse = new HttpResponse<LadderDto>
        {
            Data = new LadderDto
            {
                Format = "gen8ou",
                TopList =
                [
                    new LadderPlayerDto { UserId = "player1", Username = "player1", Elo = 1512 },
                    new LadderPlayerDto { UserId = "player2", Username = "player2", Elo = 1588 }
                ]
            }
        };

        _httpService.SendAsync<LadderDto>(Arg.Any<HttpRequest>())
            .Returns(firstResponse, secondResponse);
        _formatsManager.GetCleanFormat("gen8ou").Returns("[Gen 8] OU");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<LadderTableViewModel>())
            .Returns(Task.FromResult("formatted_html"));
        _context.Target.Returns("gen8ou");

        // Act
        await _ladderCommand.RunAsync(_context);
        await _ladderCommand.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Showdown/Ladder/LadderTable",
            Arg.Is<LadderTableViewModel>(vm =>
                vm.TopList.Any(player => player.UserId == "player1" && player.EloDifference == 12) &&
                vm.TopList.Any(player => player.UserId == "player2" && player.EloDifference == -12) &&
                vm.TopList.All(player => player.IndexDifference == 0)));
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetPlacementTrend_WhenSameLadderIsRequestedTwiceWithoutPrefix()
    {
        // Arrange
        var firstResponse = new HttpResponse<LadderDto>
        {
            Data = new LadderDto
            {
                Format = "gen8ou",
                TopList =
                [
                    new LadderPlayerDto { UserId = "player1", Username = "player1", Elo = 1500 },
                    new LadderPlayerDto { UserId = "player2", Username = "player2", Elo = 1500 }
                ]
            }
        };

        var secondResponse = new HttpResponse<LadderDto>
        {
            Data = new LadderDto
            {
                Format = "gen8ou",
                TopList =
                [
                    new LadderPlayerDto { UserId = "player2", Username = "player2", Elo = 1500 },
                    new LadderPlayerDto { UserId = "player1", Username = "player1", Elo = 1500 }
                ]
            }
        };

        _httpService.SendAsync<LadderDto>(Arg.Any<HttpRequest>())
            .Returns(firstResponse, secondResponse);
        _formatsManager.GetCleanFormat("gen8ou").Returns("[Gen 8] OU");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<LadderTableViewModel>())
            .Returns(Task.FromResult("formatted_html"));
        _context.Target.Returns("gen8ou");

        // Act
        await _ladderCommand.RunAsync(_context);
        await _ladderCommand.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Showdown/Ladder/LadderTable",
            Arg.Is<LadderTableViewModel>(vm =>
                vm.TopList.Any(player => player.UserId == "player1" && player.IndexDifference == -1) &&
                vm.TopList.Any(player => player.UserId == "player2" && player.IndexDifference == 1)));
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetPrefixedPlacementTrend_WhenSameLadderAndPrefixAreRequestedTwice()
    {
        // Arrange
        var firstResponse = new HttpResponse<LadderDto>
        {
            Data = new LadderDto
            {
                Format = "gen8ou",
                TopList =
                [
                    new LadderPlayerDto { UserId = "plaone", Username = "plaone", Elo = 1500 },
                    new LadderPlayerDto { UserId = "other", Username = "other", Elo = 1500 },
                    new LadderPlayerDto { UserId = "platwo", Username = "platwo", Elo = 1500 }
                ]
            }
        };

        var secondResponse = new HttpResponse<LadderDto>
        {
            Data = new LadderDto
            {
                Format = "gen8ou",
                TopList =
                [
                    new LadderPlayerDto { UserId = "platwo", Username = "platwo", Elo = 1500 },
                    new LadderPlayerDto { UserId = "other", Username = "other", Elo = 1500 },
                    new LadderPlayerDto { UserId = "plaone", Username = "plaone", Elo = 1500 }
                ]
            }
        };

        _httpService.SendAsync<LadderDto>(Arg.Any<HttpRequest>())
            .Returns(firstResponse, secondResponse);
        _formatsManager.GetCleanFormat("gen8ou").Returns("[Gen 8] OU");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<LadderTableViewModel>())
            .Returns(Task.FromResult("formatted_html"));
        _context.Target.Returns("gen8ou,pla");

        // Act
        await _ladderCommand.RunAsync(_context);
        await _ladderCommand.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync("Showdown/Ladder/LadderTable",
            Arg.Is<LadderTableViewModel>(vm =>
                vm.ShowInnerRanking &&
                vm.TopList.Any(player => player.UserId == "plaone" && player.IndexDifference == -2 &&
                                         player.InnerIndexDifference == -1) &&
                vm.TopList.Any(player => player.UserId == "platwo" && player.IndexDifference == 2 &&
                                         player.InnerIndexDifference == 1)));
    }
}
