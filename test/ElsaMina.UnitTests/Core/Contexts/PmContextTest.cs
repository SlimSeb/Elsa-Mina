using System.Globalization;
using ElsaMina.Core;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.UserDetails;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Contexts;

public class PmContextTests
{
    private IConfiguration _configuration;
    private IResourcesService _resourcesService;
    private IRoomsManager _roomsManager;
    private IUserDetailsManager _userDetailsManager;
    private IBot _bot;
    private IUser _sender;
    private string _message;
    private string _target;
    private string _command;

    [SetUp]
    public void SetUp()
    {
        _configuration = Substitute.For<IConfiguration>();
        _resourcesService = Substitute.For<IResourcesService>();
        _roomsManager = Substitute.For<IRoomsManager>();
        _userDetailsManager = Substitute.For<IUserDetailsManager>();
        _bot = Substitute.For<IBot>();
        _sender = Substitute.For<IUser>();
        _message = "Test message";
        _target = "Test target";
        _command = "Test command";

        _configuration.DefaultLocaleCode.Returns("");
        _configuration.DefaultRoom.Returns("TestRoom");
    }

    private PmContext CreatePmContext() =>
        new(_configuration, _resourcesService, _roomsManager, _userDetailsManager,
            _bot, _message, _target, _sender, _command);

    [Test]
    public void Test_PmContext_ShouldHavePrivateMessageFlag()
    {
        Assert.That(CreatePmContext().IsPrivateMessage, Is.True);
    }

    [Test]
    public void Test_PmContext_ShouldReturnCorrectRoomId()
    {
        Assert.That(CreatePmContext().RoomId, Is.EqualTo("TestRoom"));
    }

    [Test]
    public void Test_PmContext_ShouldReturnCorrectCulture()
    {
        Assert.That(CreatePmContext().Culture, Is.EqualTo(CultureInfo.InvariantCulture));
    }

    [Test]
    public void Test_PmContext_ShouldCallBotSendForReply()
    {
        // Arrange
        var context = CreatePmContext();
        var replyMessage = "Test reply";

        // Act
        context.Reply(replyMessage);

        // Assert
        _bot.Received().Send($"|/pm {_sender.UserId}, {replyMessage}");
    }

    [Test]
    public void Test_PmContext_ShouldCallBotSendForSendHtml()
    {
        // Arrange
        var context = CreatePmContext();
        var htmlContent = "<div>Test HTML</div>";

        // Act
        context.ReplyHtml(htmlContent);

        // Assert
        _bot.Received().Say("TestRoom", $"/pminfobox {_sender.UserId}, {htmlContent}");
    }

    [Test]
    public void Test_PmContext_ShouldCallBotSendForSendUpdatableHtml()
    {
        // Arrange
        var context = CreatePmContext();
        var htmlId = "123";
        var htmlContent = "<div>Updatable HTML</div>";

        // Act
        context.SendUpdatableHtml(htmlId, htmlContent, true);

        // Assert
        _bot.Received().Say("TestRoom", $"/pmuhtmlchange {_sender.UserId}, {htmlId}, {htmlContent}");
    }

    [Test]
    public void Test_PmContext_ShouldAlwaysHaveSufficientRank([Values] Rank rank)
    {
        Assert.That(CreatePmContext().HasRankOrHigher(rank), Is.True);
    }
}
