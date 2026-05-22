using System.Globalization;
using ElsaMina.Commands.Misc.RandomImages;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.RandomImages;

[TestFixture]
public class TenorSearchCommandTest
{
    private ITenorService _tenorService;
    private IConfiguration _configuration;
    private ITemplatesManager _templatesManager;
    private TenorSearchCommand _command;

    [SetUp]
    public void SetUp()
    {
        _tenorService = Substitute.For<ITenorService>();
        _configuration = Substitute.For<IConfiguration>();
        _templatesManager = Substitute.For<ITemplatesManager>();

        _configuration.Trigger.Returns("-");
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<html/>");

        _command = new TenorSearchCommand(_tenorService, _configuration, _templatesManager);
    }

    private IContext MakeContext(string target)
    {
        var context = Substitute.For<IContext>();
        var user = Substitute.For<IUser>();
        user.UserId.Returns("testuser");
        context.Sender.Returns(user);
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
    public void Test_IsAllowedInPrivateMessage_ShouldBeFalse()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.False);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHelpMessage_WhenTargetIsEmpty()
    {
        var context = MakeContext(string.Empty);
        context.GetString(_command.HelpMessageKey).Returns("help text");

        await _command.RunAsync(context);

        context.Received(1).Reply(Arg.Any<string>(), rankAware: Arg.Any<bool>());
        await _tenorService.DidNotReceiveWithAnyArgs().GetMultipleMediaAsync(default, default, default);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyError_WhenTenorReturnsNoResults()
    {
        var context = MakeContext("cats");
        _tenorService.GetMultipleMediaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _command.RunAsync(context);

        context.Received(1).ReplyLocalizedMessage("random_image_error");
        context.DidNotReceive().SendHtmlTo(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldSendPrivateHtml_WhenTenorReturnsResults()
    {
        var context = MakeContext("cats");
        _tenorService.GetMultipleMediaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new TenorMediaInfo("https://media.tenor.com/a.gif", 200, 100)]);

        await _command.RunAsync(context);

        context.Received(1).SendHtmlTo("testuser", Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldFetchGifsWithCorrectSearchTerm()
    {
        var context = MakeContext("  funny cats  ");
        _tenorService.GetMultipleMediaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await _command.RunAsync(context);

        await _tenorService.Received(1).GetMultipleMediaAsync(
            "funny cats", "gif", Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplate_WithCorrectViewModel()
    {
        var context = MakeContext("dogs");
        _tenorService.GetMultipleMediaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([
                new TenorMediaInfo("https://media.tenor.com/a.gif", 400, 200),
                new TenorMediaInfo("https://media.tenor.com/b.gif", 300, 150)
            ]);

        await _command.RunAsync(context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Misc/RandomImages/TenorSearch",
            Arg.Is<TenorSearchViewModel>(vm =>
                vm.Gifs.Count == 2 &&
                vm.Gifs[0].Url == "https://media.tenor.com/a.gif" &&
                vm.Gifs[0].OriginalWidth == 400 &&
                vm.Gifs[0].OriginalHeight == 200 &&
                vm.Trigger == "-"));
    }
}
