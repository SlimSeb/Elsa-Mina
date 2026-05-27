using System.Globalization;
using System.Net;
using ElsaMina.Commands.Misc.Dailymotion;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.Dailymotion;

public class DailymotionCommandTests
{
    private IHttpService _httpService;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private DailymotionCommand _command;

    [SetUp]
    public void SetUp()
    {
        _httpService = Substitute.For<IHttpService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _context = Substitute.For<IContext>();

        // Default stubs
        _context.Culture.Returns(new CultureInfo("en-US"));
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns(Task.FromResult("<div>preview</div>"));

        _command = new DailymotionCommand(_httpService, _templatesManager);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyMissingQuery_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("dailymotion_no_video_found");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendPreview_WhenNonExplicitVideoFound()
    {
        // Arrange
        _context.Target.Returns("cute cats");

        var videoList = new VideoListResponse
        {
            List =
            [
                new VideoItem { Id = "v1", Title = "explicit!", Explicit = true, ViewsTotal = 1 },
                new VideoItem
                {
                    Id = "v2", Title = "safe and popular", Explicit = false, ViewsTotal = 100,
                    ThumbnailUrl = "thumb.jpg", LikesTotal = 5
                }
            ]
        };
        var httpResult = new HttpResponse<VideoListResponse>
        {
            Data = videoList,
            StatusCode = HttpStatusCode.OK
        };

        _httpService.SendAsync<VideoListResponse>(Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(httpResult);

        // Act
        await _command.RunAsync(_context, CancellationToken.None);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
        _context.Received(1).ReplyHtml(Arg.Is<string>(s => s.Contains("preview")), rankAware: Arg.Any<bool>());
        _context.DidNotReceive().ReplyLocalizedMessage("dailymotion_no_video_found");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoVideoFound_WhenAllVideosAreExplicit()
    {
        // Arrange
        _context.Target.Returns("edgy music");

        var videoList = new VideoListResponse
        {
            List =
            [
                new VideoItem { Id = "v1", Title = "explicit1", Explicit = true, ViewsTotal = 200 },
                new VideoItem { Id = "v2", Title = "explicit2", Explicit = true, ViewsTotal = 150 }
            ]
        };
        var httpResult = new HttpResponse<VideoListResponse>
        {
            Data = videoList,
            StatusCode = HttpStatusCode.OK
        };

        _httpService.SendAsync<VideoListResponse>(Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(httpResult);

        // Act
        await _command.RunAsync(_context, CancellationToken.None);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("dailymotion_no_video_found");
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyFetchError_WhenStatusIsNotOk()
    {
        // Arrange
        _context.Target.Returns("anything");
        var exception = new HttpException(HttpStatusCode.InternalServerError, "");

        _httpService.SendAsync<VideoListResponse>(Arg.Any<HttpRequest>(),
                Arg.Any<CancellationToken>())
            .Throws(exception);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyRankAwareLocalizedMessage("dailymotion_fetch_error", Arg.Any<object>());
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }
}