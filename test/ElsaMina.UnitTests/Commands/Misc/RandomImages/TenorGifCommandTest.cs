using System.Globalization;
using ElsaMina.Commands.Misc.RandomImages;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.RandomImages;

[TestFixture]
public class TenorGifCommandTest
{
    private IImageService _imageService;
    private ITemplatesManager _templatesManager;
    private TenorGifCommand _command;

    [SetUp]
    public void SetUp()
    {
        _imageService = Substitute.For<IImageService>();
        _templatesManager = Substitute.For<ITemplatesManager>();

        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<img/>");

        _command = new TenorGifCommand(_imageService, _templatesManager);
    }

    private IContext MakeContext(string target)
    {
        var context = Substitute.For<IContext>();
        context.Target.Returns(target);
        context.Culture.Returns(CultureInfo.InvariantCulture);
        return context;
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetIsEmpty()
    {
        var context = MakeContext(string.Empty);
        context.GetString(_command.HelpMessageKey).Returns("help text");

        await _command.RunAsync(context);

        context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidUrl_WhenUrlIsNotTenorCdn()
    {
        var context = MakeContext("https://example.com/image.gif");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("tenorgif_invalid_url");
        context.DidNotReceive().ReplyHtml(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidUrl_WhenUrlIsHttp()
    {
        var context = MakeContext("http://media.tenor.com/a.gif");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("tenorgif_invalid_url");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyInvalidUrl_WhenUrlIsNotAbsolute()
    {
        var context = MakeContext("not-a-url");

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("tenorgif_invalid_url");
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseEncodedDimensions_WhenPresentInTarget()
    {
        var context = MakeContext("https://media.tenor.com/a.gif|400|200");

        await _command.RunAsync(context);

        await _imageService.DidNotReceiveWithAnyArgs().GetRemoteImageDimensions(default);
        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/RandomImages/TenorGif",
            Arg.Is<TenorGifViewModel>(vm =>
                vm.Url == "https://media.tenor.com/a.gif" &&
                vm.Width == 200 &&
                vm.Height == 100));
    }

    [Test]
    public async Task Test_RunAsync_ShouldFetchDimensionsFromImageService_WhenNotEncodedInTarget()
    {
        var context = MakeContext("https://media.tenor.com/a.gif");
        _imageService.GetRemoteImageDimensions(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((600, 300));

        await _command.RunAsync(context);

        await _imageService.Received(1).GetRemoteImageDimensions(
            "https://media.tenor.com/a.gif", Arg.Any<CancellationToken>());
        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/RandomImages/TenorGif",
            Arg.Is<TenorGifViewModel>(vm => vm.Width == 300 && vm.Height == 150));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtml_WithRenderedTemplate()
    {
        var context = MakeContext("https://media.tenor.com/a.gif|200|100");

        await _command.RunAsync(context);

        context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: false);
    }
}
