using System.Net;
using ElsaMina.Commands.Misc.UrlPreview;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Rooms.Parameters;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.UrlPreview;

public class UrlPreviewHandlerTest
{
    private UrlPreviewHandler _handler;
    private IHttpService _httpService;
    private IConfiguration _configuration;
    private ITemplatesManager _templatesManager;
    private IImageService _imageService;
    private IContext _context;
    private IRoom _room;

    [SetUp]
    public void SetUp()
    {
        var contextFactory = Substitute.For<IContextFactory>();
        _httpService = Substitute.For<IHttpService>();
        _configuration = Substitute.For<IConfiguration>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _imageService = Substitute.For<IImageService>();
        _context = Substitute.For<IContext>();
        _room = Substitute.For<IRoom>();

        _context.Room.Returns(_room);
        _configuration.Name.Returns("botname");
        _configuration.Trigger.Returns("-");

        _room.GetParameterValueAsync(Parameter.ShowUrlPreview, Arg.Any<CancellationToken>())
            .Returns("true");

        _handler = new UrlPreviewHandler(contextFactory, _httpService, _configuration,
            _templatesManager, _imageService);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotProcess_WhenPreviewIsDisabled()
    {
        // Arrange
        _room.GetParameterValueAsync(Parameter.ShowUrlPreview, Arg.Any<CancellationToken>())
            .Returns("false");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs().SendForStringAsync(default);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotProcess_WhenSenderIsBot()
    {
        // Arrange
        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("botname");
        _context.Sender.Returns(sender);
        _context.Message.Returns("https://example.com");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs().SendForStringAsync(default);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotProcess_WhenMessageIsCommand()
    {
        // Arrange
        _context.Message.Returns("-somecommand https://example.com");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs().SendForStringAsync(default);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotProcess_WhenMessageHasNoUrl()
    {
        // Arrange
        _context.Message.Returns("just a plain message with no link");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs().SendForStringAsync(default);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotProcess_WhenUrlIsYoutube()
    {
        // Arrange
        _context.Message.Returns("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs().SendForStringAsync(default);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotProcess_WhenUrlIsShowdownReplay()
    {
        // Arrange
        _context.Message.Returns("https://replay.pokemonshowdown.com/gen8ou-123456789");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _httpService.DidNotReceiveWithAnyArgs().SendForStringAsync(default);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotProcess_WhenHttpResponseIsNotOk()
    {
        // Arrange
        _context.Message.Returns("https://example.com");
        _httpService.SendForStringAsync(Arg.Any<HttpRequest>())
            .Returns(new HttpResponse<string> { StatusCode = HttpStatusCode.NotFound, Data = null });

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotProcess_WhenPageHasNoTitleOrOgTitle()
    {
        // Arrange
        _context.Message.Returns("https://example.com");
        _httpService.SendForStringAsync(Arg.Any<HttpRequest>())
            .Returns(new HttpResponse<string>
            {
                StatusCode = HttpStatusCode.OK,
                Data = "<html><body>No title here</body></html>"
            });

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldReplyHtml_WhenOgTagsArePresent()
    {
        // Arrange
        _context.Message.Returns("https://example.com");
        _httpService.SendForStringAsync(Arg.Any<HttpRequest>())
            .Returns(new HttpResponse<string>
            {
                StatusCode = HttpStatusCode.OK,
                Data = """
                       <html>
                       <head>
                       <meta property="og:title" content="Example Title" />
                       <meta property="og:description" content="Example description" />
                       <meta property="og:site_name" content="Example Site" />
                       </head>
                       </html>
                       """
            });
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<UrlPreviewViewModel>())
            .Returns("rendered-html");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/UrlPreview/UrlPreview",
            Arg.Is<UrlPreviewViewModel>(vm =>
                vm.Title == "Example Title" &&
                vm.Description == "Example description" &&
                vm.SiteName == "Example Site"));
        _context.Received(1).ReplyHtml("rendered-html");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldFallbackToTitleTag_WhenNoOgTitle()
    {
        // Arrange
        _context.Message.Returns("Check out https://example.com");
        _httpService.SendForStringAsync(Arg.Any<HttpRequest>())
            .Returns(new HttpResponse<string>
            {
                StatusCode = HttpStatusCode.OK,
                Data = "<html><head><title>Fallback Title</title></head></html>"
            });
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<UrlPreviewViewModel>())
            .Returns("rendered-html");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/UrlPreview/UrlPreview",
            Arg.Is<UrlPreviewViewModel>(vm => vm.Title == "Fallback Title"));
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldFetchImageDimensions_WhenOgImageIsPresent()
    {
        // Arrange
        _context.Message.Returns("https://example.com");
        _httpService.SendForStringAsync(Arg.Any<HttpRequest>())
            .Returns(new HttpResponse<string>
            {
                StatusCode = HttpStatusCode.OK,
                Data = """
                       <meta property="og:title" content="Title" />
                       <meta property="og:image" content="https://example.com/image.jpg" />
                       """
            });
        _imageService.GetRemoteImageDimensions("https://example.com/image.jpg", Arg.Any<CancellationToken>())
            .Returns((400, 300));
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<UrlPreviewViewModel>())
            .Returns("rendered-html");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _imageService.Received(1)
            .GetRemoteImageDimensions("https://example.com/image.jpg", Arg.Any<CancellationToken>());
        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/UrlPreview/UrlPreview",
            Arg.Is<UrlPreviewViewModel>(vm => vm.ImageUrl == "https://example.com/image.jpg"));
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotReplyHtml_WhenExceptionOccurs()
    {
        // Arrange
        _context.Message.Returns("https://example.com");
        _httpService.SendForStringAsync(Arg.Any<HttpRequest>())
            .Throws(new Exception("Network error"));

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>());
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldTrimTrailingPunctuation_FromDetectedUrl()
    {
        // Arrange
        _context.Message.Returns("Check this out: https://example.com/page.");
        _httpService.SendForStringAsync(Arg.Any<HttpRequest>())
            .Returns(new HttpResponse<string>
            {
                StatusCode = HttpStatusCode.OK,
                Data = "<title>Page Title</title>"
            });
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<UrlPreviewViewModel>())
            .Returns("rendered-html");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/UrlPreview/UrlPreview",
            Arg.Is<UrlPreviewViewModel>(vm => vm.Url == "https://example.com/page"));
    }
}
