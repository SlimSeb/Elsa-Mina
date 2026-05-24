using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.AddedCommands;
using ElsaMina.Core.Services.Images;
using ElsaMina.Core.Services.Probabilities;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using User = ElsaMina.Core.Services.Rooms.User;

namespace ElsaMina.UnitTests.Core.Services.AddedCommands;

public class AddedCommandsManagerTest
{
    private IBotDbContextFactory _factory;
    private BotDbContext _db;
    private AddedCommandsManager _manager;
    private IImageService _imageService;
    private IRandomService _randomService;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // isolate each test
            .Options;

        _db = new BotDbContext(options);

        _factory = Substitute.For<IBotDbContextFactory>();
        _factory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(_db));

        _imageService = Substitute.For<IImageService>();
        _randomService = Substitute.For<IRandomService>();

        _manager = new AddedCommandsManager(_factory, _imageService, _randomService);
    }

    private async Task AddCommandAsync(string name, string roomId, string content)
    {
        _db.AddedCommands.Add(new AddedCommand
        {
            Id = name,
            RoomId = roomId,
            Content = content
        });

        await _db.SaveChangesAsync();
    }

    [Test]
    public async Task Test_TryExecuteAddedCommand_ShouldDoNothing_WhenCommandNotFound()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.RoomId.Returns("franais");

        // Act
        await _manager.TryExecuteAddedCommand("doesNotExist", context, CancellationToken.None);

        // Assert
        context.DidNotReceive().Reply(Arg.Any<string>(), Arg.Any<bool>());
        context.DidNotReceive().ReplyHtml(Arg.Any<string>(), rankAware: Arg.Any<bool>());
    }

    [Test]
    public async Task Test_TryExecuteAddedCommand_ShouldSendImage_WhenContentIsImageUrl()
    {
        // Arrange
        var context = Substitute.For<IContext>();
        context.RoomId.Returns("franais");
        await AddCommandAsync("imageCommand", "franais", "https://example.com/image.png");

        _imageService.GetRemoteImageDimensions(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((400, 300)));

        // Act
        await _manager.TryExecuteAddedCommand("imageCommand", context, CancellationToken.None);

        // Assert
        context.Received().ReplyHtml(
            Arg.Is<string>(s => s.Contains("https://example.com/image.png")),
            rankAware: true);
    }

    [Test]
    public async Task Test_TryExecuteAddedCommand_ShouldReplyWithContent_WhenContentIsText()
    {
        var context = Substitute.For<IContext>();
        context.RoomId.Returns("franais");

        await AddCommandAsync("text", "franais", "Hello!");

        await _manager.TryExecuteAddedCommand("text", context, CancellationToken.None);

        context.Received().Reply("Hello!", true);
    }

    [Test]
    public async Task Test_TryExecuteAddedCommand_ShouldResizeImage_WhenLarge()
    {
        var context = Substitute.For<IContext>();
        context.RoomId.Returns("franais");

        await AddCommandAsync("large", "franais", "https://example.com/large.png");

        _imageService.GetRemoteImageDimensions("https://example.com/large.png", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((800, 600)));

        await _manager.TryExecuteAddedCommand("large", context, CancellationToken.None);

        context.Received().ReplyHtml(
            Arg.Is<string>(s => s.Contains("width=\"400\" height=\"300\"")),
            rankAware: true);
    }

    [Test]
    public async Task Test_TryExecuteAddedCommand_ShouldNotResizeImage_WhenSmall()
    {
        var context = Substitute.For<IContext>();
        context.RoomId.Returns("franais");

        await AddCommandAsync("small", "franais", "https://example.com/small.png");

        _imageService.GetRemoteImageDimensions("https://example.com/small.png", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((200, 150)));

        await _manager.TryExecuteAddedCommand("small", context, CancellationToken.None);

        context.Received().ReplyHtml(
            Arg.Is<string>(s => s.Contains("width=\"200\" height=\"150\"")),
            rankAware: true);
    }

    [Test]
    public async Task Test_ExecuteAddedCommand_ShouldReplyWithContent_WhenContentIsText()
    {
        var context = Substitute.For<IContext>();
        var command = new AddedCommand { Id = "cmd", RoomId = "franais", Content = "Hello!" };

        await _manager.ExecuteAddedCommand(command, context, CancellationToken.None);

        context.Received().Reply("Hello!", true);
    }

    [Test]
    public async Task Test_ExecuteAddedCommand_ShouldSendImage_WhenContentIsImageUrl()
    {
        var context = Substitute.For<IContext>();
        var command = new AddedCommand { Id = "img", RoomId = "franais", Content = "https://example.com/image.png" };

        _imageService.GetRemoteImageDimensions(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult((400, 300)));

        await _manager.ExecuteAddedCommand(command, context, CancellationToken.None);

        context.Received().ReplyHtml(
            Arg.Is<string>(s => s.Contains("https://example.com/image.png")),
            rankAware: true);
    }

    [Test]
    public void Test_ParseContent_ShouldParseExpression()
    {
        var context = Substitute.For<IContext>();
        const string content = "value = {sin(123) + cos(456) + tan(789)} {repeat(sin(e), 3)}";

        var result = _manager.EvaluateContent(content, context);

        Assert.That(result, Is.EqualTo(
            "value = -0.8561420972077014 0.410781290502908850.410781290502908850.41078129050290885"));
    }

    [Test]
    public void Test_ParseContent_ShouldParseExpressionWithContext()
    {
        var room = Substitute.For<IRoom>();
        room.RoomId.Returns("franais");
        room.Name.Returns("Français");
        var context = Substitute.For<IContext>();
        context.Sender.Returns(new User("Mec", Rank.Voiced));
        context.Room.Returns(room);
        context.Command.Returns("myCmd");
        context.Target.Returns("myArgs");

        var result = _manager.EvaluateContent("{command} {author} {room} {args}", context);

        Assert.That(result, Is.EqualTo("myCmd Mec Français myArgs"));
    }
}
