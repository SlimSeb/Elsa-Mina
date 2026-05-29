using ElsaMina.Commands.Misc.Help;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Misc.Help;

public class HelpHandlerTest
{
    private IContextFactory _contextFactory;
    private IConfiguration _configuration;
    private IContext _context;
    private IUser _sender;
    private HelpHandler _handler;

    [SetUp]
    public void SetUp()
    {
        _contextFactory = Substitute.For<IContextFactory>();
        _configuration = Substitute.For<IConfiguration>();
        _context = Substitute.For<IContext>();
        _sender = Substitute.For<IUser>();

        _context.Sender.Returns(_sender);
        _sender.UserId.Returns("someuser");
        _context.Message.Returns("hello there");

        _configuration.Name.Returns("Elsa-Mina");
        _configuration.Trigger.Returns("-");

        _handler = new HelpHandler(_contextFactory, _configuration);
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldReplyGreeting_WhenMessageIsNotACommandAndSenderIsNotBot()
    {
        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("help_handler_greeting", "Elsa-Mina", "-",
            "https://github.com/SlimSeb/Elsa-Mina");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotReply_WhenMessageStartsWithTrigger()
    {
        // Arrange
        _context.Message.Returns("-help");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotReply_WhenSenderIsTheBotItself()
    {
        // Arrange
        _sender.UserId.Returns("elsamina");

        // Act
        await _handler.HandleMessageAsync(_context);

        // Assert
        _context.DidNotReceive().ReplyLocalizedMessage(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldNotReplyTwice_WhenSameUserMessagesAgain()
    {
        // Act
        await _handler.HandleMessageAsync(_context);
        await _handler.HandleMessageAsync(_context);

        // Assert
        _context.Received(1).ReplyLocalizedMessage("help_handler_greeting", "Elsa-Mina", "-",
            "https://github.com/SlimSeb/Elsa-Mina");
    }

    [Test]
    public async Task Test_HandleMessageAsync_ShouldReplyToEachUserOnce_WhenDifferentUsersMessage()
    {
        // Arrange
        var firstContext = Substitute.For<IContext>();
        var firstSender = Substitute.For<IUser>();
        firstSender.UserId.Returns("userone");
        firstContext.Sender.Returns(firstSender);
        firstContext.Message.Returns("hi");

        var secondContext = Substitute.For<IContext>();
        var secondSender = Substitute.For<IUser>();
        secondSender.UserId.Returns("usertwo");
        secondContext.Sender.Returns(secondSender);
        secondContext.Message.Returns("hi");

        // Act
        await _handler.HandleMessageAsync(firstContext);
        await _handler.HandleMessageAsync(secondContext);

        // Assert
        firstContext.Received(1).ReplyLocalizedMessage("help_handler_greeting", Arg.Any<object[]>());
        secondContext.Received(1).ReplyLocalizedMessage("help_handler_greeting", Arg.Any<object[]>());
    }
}
