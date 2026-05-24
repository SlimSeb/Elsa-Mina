using System.Globalization;
using ElsaMina.Commands.Misc.Genius;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ElsaMina.UnitTests.Commands.Misc.Genius;

[TestFixture]
public class GeniusSearchCommandTest
{
    private IConfiguration _configuration;
    private IHttpService _httpService;
    private ITemplatesManager _templatesManager;
    private IImageService _imageService;
    private IContext _context;
    private GeniusSearchCommand _command;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _httpService = Substitute.For<IHttpService>();
        _templatesManager = Substitute.For<ITemplatesManager>();
        _imageService = Substitute.For<IImageService>();
        _context = Substitute.For<IContext>();

        _configuration.GeniusApiKey.Returns("test-api-key");
        _context.Culture.Returns(CultureInfo.GetCultureInfo("en-US"));
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<html/>");
        _imageService.GetRemoteImageDimensions(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((100, 100));

        _command = new GeniusSearchCommand(_configuration, _httpService, _templatesManager, _imageService);
    }

    // Builds an IHttpResponse without any nested substitute calls inside Returns()
    private static IHttpResponse<GeniusSearchResult> MakeHttpResponse(params Hit[] hits)
    {
        var geniusResult = new GeniusSearchResult
        {
            Response = new Response { Hits = [..hits] }
        };
        var response = Substitute.For<IHttpResponse<GeniusSearchResult>>();
        response.Data.Returns(geniusResult);
        return response;
    }

    private static Hit MakeSongHit(
        string title, string artistNames, int pageviews,
        ReleaseDateComponents releaseDateComponents = null,
        string thumbnailUrl = "https://img.example.com/thumb.jpg",
        string lyricsUrl = "https://genius.com/song")
    {
        return new Hit
        {
            Type = "song",
            Result = new Result
            {
                Title = title,
                ArtistNames = artistNames,
                SongArtImageUrl = thumbnailUrl,
                Url = lyricsUrl,
                Stats = new Stats { Pageviews = pageviews },
                ReleaseDateComponents = releaseDateComponents
            }
        };
    }

    private void SetupHttpResponse(IHttpResponse<GeniusSearchResult> response)
    {
        _httpService.GetAsync<GeniusSearchResult>(
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, string>>(),
                Arg.Any<IDictionary<string, string>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(response);
    }

    // Properties

    [Test]
    public void Test_RequiredRank_ShouldBeVoiced()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Voiced));
    }

    [Test]
    public void Test_HelpMessageKey_ShouldBeGeniusHelpMessage()
    {
        Assert.That(_command.HelpMessageKey, Is.EqualTo("genius_help_message"));
    }

    // API key guard

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenApiKeyIsNull()
    {
        // Arrange
        _configuration.GeniusApiKey.Returns((string)null);
        _context.Target.Returns("bohemian rhapsody");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _httpService.DidNotReceive()
            .GetAsync<GeniusSearchResult>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                Arg.Any<IDictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<bool>(),
                Arg.Any<CancellationToken>());
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldDoNothing_WhenApiKeyIsWhitespace()
    {
        // Arrange
        _configuration.GeniusApiKey.Returns("   ");
        _context.Target.Returns("bohemian rhapsody");

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _httpService.DidNotReceive()
            .GetAsync<GeniusSearchResult>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                Arg.Any<IDictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<bool>(),
                Arg.Any<CancellationToken>());
    }

    // Target guard

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetIsEmpty()
    {
        // Arrange
        _context.Target.Returns(string.Empty);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        await _httpService.DidNotReceive()
            .GetAsync<GeniusSearchResult>(Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                Arg.Any<IDictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<bool>(),
                Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetIsWhitespace()
    {
        // Arrange
        _context.Target.Returns("   ");

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    // No results

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenHitsListIsEmpty()
    {
        // Arrange
        _context.Target.Returns("unknown xyzzy");
        var emptyResponse = MakeHttpResponse();
        SetupHttpResponse(emptyResponse);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("genius_not_found");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNotFound_WhenNoHitsAreOfTypeSong()
    {
        // Arrange
        _context.Target.Returns("something");
        var artistHit = new Hit { Type = "artist", Result = new Result { Stats = new Stats { Pageviews = 100 } } };
        var artistOnlyResponse = MakeHttpResponse(artistHit);
        SetupHttpResponse(artistOnlyResponse);

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("genius_not_found");
    }

    // Hit selection

    [Test]
    public async Task Test_RunAsync_ShouldSelectMostViewedSongHit_WhenMultipleHitsExist()
    {
        // Arrange
        _context.Target.Returns("queen");
        var lessViewed = MakeSongHit("Another One Bites the Dust", "Queen", pageviews: 500_000);
        var mostViewed = MakeSongHit("Bohemian Rhapsody", "Queen", pageviews: 2_000_000);
        SetupHttpResponse(MakeHttpResponse(lessViewed, mostViewed));

        GeniusSongPanelViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<GeniusSongPanelViewModel>(vm => capturedViewModel = vm));

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedViewModel, Is.Not.Null);
            Assert.That(capturedViewModel.Title, Is.EqualTo("Bohemian Rhapsody"));
        }
    }

    // Thumbnail dimensions

    [Test]
    public async Task Test_RunAsync_ShouldUseFallbackDimensions_WhenImageServiceReturnsNegativeValues()
    {
        // Arrange
        _context.Target.Returns("song");
        SetupHttpResponse(MakeHttpResponse(MakeSongHit("Title", "Artist", pageviews: 1000)));
        _imageService.GetRemoteImageDimensions(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((-1, -1));

        GeniusSongPanelViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<GeniusSongPanelViewModel>(vm => capturedViewModel = vm));

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedViewModel, Is.Not.Null);
            Assert.That(capturedViewModel.ThumbnailWidth, Is.EqualTo(115));
            Assert.That(capturedViewModel.ThumbnailHeight, Is.EqualTo(115));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldResizeDimensions_WhenImageIsTooLarge()
    {
        // Arrange
        _context.Target.Returns("song");
        SetupHttpResponse(MakeHttpResponse(MakeSongHit("Title", "Artist", pageviews: 1000)));
        _imageService.GetRemoteImageDimensions(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((460, 460));

        GeniusSongPanelViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<GeniusSongPanelViewModel>(vm => capturedViewModel = vm));

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedViewModel, Is.Not.Null);
            Assert.That(capturedViewModel.ThumbnailWidth, Is.LessThanOrEqualTo(115));
            Assert.That(capturedViewModel.ThumbnailHeight, Is.LessThanOrEqualTo(115));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldKeepOriginalDimensions_WhenImageIsSmallEnough()
    {
        // Arrange
        _context.Target.Returns("song");
        SetupHttpResponse(MakeHttpResponse(MakeSongHit("Title", "Artist", pageviews: 1000)));
        _imageService.GetRemoteImageDimensions(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((80, 80));

        GeniusSongPanelViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<GeniusSongPanelViewModel>(vm => capturedViewModel = vm));

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedViewModel, Is.Not.Null);
            Assert.That(capturedViewModel.ThumbnailWidth, Is.EqualTo(80));
            Assert.That(capturedViewModel.ThumbnailHeight, Is.EqualTo(80));
        }
    }

    // Release date

    [Test]
    public async Task Test_RunAsync_ShouldSetReleaseDate_WhenAllDateComponentsArePresent()
    {
        // Arrange
        _context.Target.Returns("bohemian rhapsody");
        var hit = MakeSongHit("Bohemian Rhapsody", "Queen", pageviews: 1000,
            releaseDateComponents: new ReleaseDateComponents { Year = 1975, Month = 10, Day = 31 });
        SetupHttpResponse(MakeHttpResponse(hit));

        GeniusSongPanelViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<GeniusSongPanelViewModel>(vm => capturedViewModel = vm));

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedViewModel, Is.Not.Null);
            Assert.That(capturedViewModel.ReleaseDate, Is.Not.Empty);
            Assert.That(capturedViewModel.ReleaseDate, Does.Contain("1975"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetEmptyReleaseDate_WhenDateComponentsAreNull()
    {
        // Arrange
        _context.Target.Returns("song");
        var hit = MakeSongHit("Title", "Artist", pageviews: 1000, releaseDateComponents: null);
        SetupHttpResponse(MakeHttpResponse(hit));

        GeniusSongPanelViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<GeniusSongPanelViewModel>(vm => capturedViewModel = vm));

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedViewModel, Is.Not.Null);
            Assert.That(capturedViewModel.ReleaseDate, Is.Empty);
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetEmptyReleaseDate_WhenDateComponentsAreIncomplete()
    {
        // Arrange
        _context.Target.Returns("song");
        var hit = MakeSongHit("Title", "Artist", pageviews: 1000,
            releaseDateComponents: new ReleaseDateComponents { Year = 1975, Month = null, Day = 31 });
        SetupHttpResponse(MakeHttpResponse(hit));

        GeniusSongPanelViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<GeniusSongPanelViewModel>(vm => capturedViewModel = vm));

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedViewModel, Is.Not.Null);
            Assert.That(capturedViewModel.ReleaseDate, Is.Empty);
        }
    }

    // View model fields

    [Test]
    public async Task Test_RunAsync_ShouldPopulateViewModelFields_WhenSongIsFound()
    {
        // Arrange
        _context.Target.Returns("stairway to heaven");
        var hit = MakeSongHit(
            title: "Stairway to Heaven",
            artistNames: "Led Zeppelin",
            pageviews: 5_000_000,
            thumbnailUrl: "https://img.example.com/led.jpg",
            lyricsUrl: "https://genius.com/led-zeppelin-stairway");
        SetupHttpResponse(MakeHttpResponse(hit));

        GeniusSongPanelViewModel capturedViewModel = null;
        await _templatesManager.GetTemplateAsync(Arg.Any<string>(),
            Arg.Do<GeniusSongPanelViewModel>(vm => capturedViewModel = vm));

        // Act
        await _command.RunAsync(_context);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(capturedViewModel, Is.Not.Null);
            Assert.That(capturedViewModel.Title, Is.EqualTo("Stairway to Heaven"));
            Assert.That(capturedViewModel.ArtistName, Is.EqualTo("Led Zeppelin"));
            Assert.That(capturedViewModel.ThumbnailUrl, Is.EqualTo("https://img.example.com/led.jpg"));
            Assert.That(capturedViewModel.LyricsUrl, Is.EqualTo("https://genius.com/led-zeppelin-stairway"));
            Assert.That(capturedViewModel.PageViews, Is.EqualTo(5_000_000));
        }
    }

    // Template rendering and reply

    [Test]
    public async Task Test_RunAsync_ShouldRenderGeniusSongPanelTemplate_WhenSongIsFound()
    {
        // Arrange
        _context.Target.Returns("song");
        SetupHttpResponse(MakeHttpResponse(MakeSongHit("Title", "Artist", pageviews: 1000)));

        // Act
        await _command.RunAsync(_context);

        // Assert
        await _templatesManager.Received(1)
            .GetTemplateAsync("Misc/Genius/GeniusSongPanel", Arg.Any<GeniusSongPanelViewModel>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldCallReplyHtml_WhenSongIsFound()
    {
        // Arrange
        _context.Target.Returns("song");
        SetupHttpResponse(MakeHttpResponse(MakeSongHit("Title", "Artist", pageviews: 1000)));

        // Act
        await _command.RunAsync(_context);

        // Assert
        _context.Received(1).ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    // Exception handling

    [Test]
    public async Task Test_RunAsync_ShouldSwallowException_WhenHttpServiceThrows()
    {
        // Arrange
        _context.Target.Returns("song");
        _httpService.GetAsync<GeniusSearchResult>(
                Arg.Any<string>(), Arg.Any<IDictionary<string, string>>(),
                Arg.Any<IDictionary<string, string>>(), Arg.Any<bool>(), Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert - exception must not propagate
        Assert.DoesNotThrowAsync(() => _command.RunAsync(_context));
        _context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }
}
