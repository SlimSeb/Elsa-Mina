using ElsaMina.Commands.Misc.Youtube;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.Youtube;

public class YoutubeVideoOnLinkHandlerTest
{
    private YoutubeVideoOnLinkHandler _handler;
    private IHttpService _httpService;
    private IConfiguration _configuration;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private IRoom _room;

    [SetUp]
    public void SetUp()
    {
        var contextFactory = Substitute.For<IContextFactory>();
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();
        _context.Room.Returns(_room);

        _handler = new YoutubeVideoOnLinkHandler(contextFactory, _httpService, _configuration, _templatesManager);
        _configuration.Name.Returns("bot");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotProcess_WhenPreviewDisabled()
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.ShowYoutubeLinkPreview, Arg.Any<CancellationToken>())
            .Returns("false");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs()
            .GetAsync<YouTubeVideoListResponse>(default, default);
    }

    [Test]
    [TestCase("!show https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [TestCase("!show https://youtu.be/dQw4w9WgXcQ")]
    public async Task Test_HandleMessageAsync_ShouldNotProcess_WhenMessageStartsWithShowCommand(string message)
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.ShowYoutubeLinkPreview, Arg.Any<CancellationToken>())
            .Returns("true");
        _context.Message.Returns(message);

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs()
            .GetAsync<YouTubeVideoListResponse>(default, default);
    }

    [Test]
    [TestCase("This is a message without any link.")]
    [TestCase("https://www.google.com/search?q=hello")]
    [TestCase("https://vimeo.com/123456789")]
    public async Task Test_HandleMessageAsync_ShouldNotProcess_WhenNoYoutubeLinkFound(string message)
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.ShowYoutubeLinkPreview, Arg.Any<CancellationToken>())
            .Returns("true");
        _context.Message.Returns(message);

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs()
            .GetAsync<YouTubeVideoListResponse>(default, default);
    }

    [Test]
    [TestCase(null)]
    [TestCase("")]
    public async Task Test_HandleMessageAsync_ShouldNotProcess_WhenApiKeyIsEmpty(string emptyApiKey)
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.ShowYoutubeLinkPreview, Arg.Any<CancellationToken>())
            .Returns("true");
        _context.Message.Returns("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        _configuration.YoutubeApiKey.Returns(emptyApiKey);

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs()
            .GetAsync<YouTubeVideoListResponse>(default, default);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotReply_WhenApiReturnsNoItems()
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.ShowYoutubeLinkPreview, Arg.Any<CancellationToken>())
            .Returns("true");
        _context.Message.Returns("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        _configuration.YoutubeApiKey.Returns("fakeApiKey");
        _httpService.GetAsync<YouTubeVideoListResponse>(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Returns(new HttpResponse<YouTubeVideoListResponse> { Data = new YouTubeVideoListResponse { Items = [] } });

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>());
    }

    [Test]
    [TestCase("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [TestCase("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [TestCase("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [TestCase("https://m.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    public async Task Test_HandleMessageAsync_ShouldFetchCorrectVideoId_WhenVariousUrlFormatsPosted(
        string url, string expectedVideoId)
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.ShowYoutubeLinkPreview, Arg.Any<CancellationToken>())
            .Returns("true");
        _context.Message.Returns(url);
        _configuration.YoutubeApiKey.Returns("fakeApiKey");
        _httpService.GetAsync<YouTubeVideoListResponse>(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Returns(new HttpResponse<YouTubeVideoListResponse>
            {
                Data = new YouTubeVideoListResponse
                {
                    Items =
                    [
                        new YouTubeVideoItem
                        {
                            Id = expectedVideoId,
                            Snippet = new Snippet
                            {
                                Title = "Test",
                                ChannelTitle = "Channel",
                                Description = "Desc",
                                PublishedAt = "2020-01-01T00:00:00Z",
                                Thumbnails = new Thumbnails { Medium = new Thumbnail { Url = "https://img.com/thumb.jpg" } }
                            }
                        }
                    ]
                }
            });
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<YoutubeVideoPreviewViewModel>())
            .Returns("html");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/Youtube/YoutubeVideoPreview",
            Arg.Is<YoutubeVideoPreviewViewModel>(vm => vm.VideoId == expectedVideoId));
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldSendHtmlResponse_WhenVideoInfoFetchedSuccessfully()
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.ShowYoutubeLinkPreview, Arg.Any<CancellationToken>())
            .Returns("true");
        _context.Message.Returns("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        _configuration.YoutubeApiKey.Returns("fakeApiKey");
        _httpService.GetAsync<YouTubeVideoListResponse>(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Returns(new HttpResponse<YouTubeVideoListResponse>
            {
                Data = new YouTubeVideoListResponse
                {
                    Items =
                    [
                        new YouTubeVideoItem
                        {
                            Id = "dQw4w9WgXcQ",
                            Snippet = new Snippet
                            {
                                Title = "Never Gonna Give You Up",
                                ChannelTitle = "Rick Astley",
                                Description = "The classic.",
                                PublishedAt = "2009-10-25T06:57:33Z",
                                Thumbnails = new Thumbnails
                                {
                                    Medium = new Thumbnail { Url = "https://img.youtube.com/vi/dQw4w9WgXcQ/mqdefault.jpg" }
                                }
                            }
                        }
                    ]
                }
            });
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<YoutubeVideoPreviewViewModel>())
            .Returns("rendered-html");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/Youtube/YoutubeVideoPreview",
            Arg.Is<YoutubeVideoPreviewViewModel>(vm =>
                vm.VideoId == "dQw4w9WgXcQ"
                && vm.Title == "Never Gonna Give You Up"
                && vm.ChannelTitle == "Rick Astley"
                && vm.Description == "The classic."
                && vm.PublishTime == new DateTime(2009, 10, 25, 6, 57, 33, DateTimeKind.Utc)
                && vm.ThumbnailSource == "https://img.youtube.com/vi/dQw4w9WgXcQ/mqdefault.jpg"
            ));
        _context.Received(1).ReplyHtml("rendered-html");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldFallBackToDefaultThumbnail_WhenMediumThumbnailUnavailable()
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.ShowYoutubeLinkPreview, Arg.Any<CancellationToken>())
            .Returns("true");
        _context.Message.Returns("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        _configuration.YoutubeApiKey.Returns("fakeApiKey");
        _httpService.GetAsync<YouTubeVideoListResponse>(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Returns(new HttpResponse<YouTubeVideoListResponse>
            {
                Data = new YouTubeVideoListResponse
                {
                    Items =
                    [
                        new YouTubeVideoItem
                        {
                            Id = "dQw4w9WgXcQ",
                            Snippet = new Snippet
                            {
                                Title = "Test",
                                ChannelTitle = "Channel",
                                Description = "Desc",
                                PublishedAt = "2020-01-01T00:00:00Z",
                                Thumbnails = new Thumbnails
                                {
                                    Default = new Thumbnail { Url = "https://img.youtube.com/vi/dQw4w9WgXcQ/default.jpg" }
                                }
                            }
                        }
                    ]
                }
            });
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<YoutubeVideoPreviewViewModel>())
            .Returns("rendered-html");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/Youtube/YoutubeVideoPreview",
            Arg.Is<YoutubeVideoPreviewViewModel>(vm =>
                vm.ThumbnailSource == "https://img.youtube.com/vi/dQw4w9WgXcQ/default.jpg"));
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotReply_WhenExceptionOccurs()
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.ShowYoutubeLinkPreview, Arg.Any<CancellationToken>())
            .Returns("true");
        _context.Message.Returns("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        _configuration.YoutubeApiKey.Returns("fakeApiKey");
        _httpService.GetAsync<YouTubeVideoListResponse>(Arg.Any<string>(), Arg.Any<Dictionary<string, string>>())
            .Throws(new Exception("Network error"));

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>());
    }
}
