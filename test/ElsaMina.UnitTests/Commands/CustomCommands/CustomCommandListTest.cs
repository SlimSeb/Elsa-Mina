using System.Globalization;
using ElsaMina.Commands.CustomCommands;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.CustomCommands;

public class CustomCommandListTest
{
    private DbContextOptions<BotDbContext> _dbOptions;
    private IBotDbContextFactory _dbContextFactory;
    private ITemplatesManager _templatesManager;
    private IContext _context;
    private CustomCommandList _command;

    [SetUp]
    public void SetUp()
    {
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new BotDbContext(_dbOptions);
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => new BotDbContext(_dbOptions));

        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<html>template</html>");

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("testroom");
        _context.Culture.Returns(CultureInfo.GetCultureInfo("en-US"));

        _command = new CustomCommandList(_dbContextFactory, _templatesManager);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        var command = new CustomCommandList(_dbContextFactory, _templatesManager);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Name, Is.EqualTo("custom-command-list"));
            Assert.That(command.RequiredRank, Is.EqualTo(Rank.Voiced));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoCommands_WhenRoomHasNoCustomCommands()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("customcommandlist_no_commands");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoCommands_WhenRoomHasNoCommandsButOtherRoomsDo()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.AddedCommands.AddAsync(new AddedCommand
            {
                Id = "hello",
                RoomId = "otherroom",
                Content = "Hello world!",
                Author = "user1",
                CreationDate = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("customcommandlist_no_commands");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplate_WhenRoomHasCustomCommands()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.AddedCommands.AddAsync(new AddedCommand
            {
                Id = "hello",
                RoomId = "testroom",
                Content = "Hello world!",
                Author = "alice",
                CreationDate = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "CustomCommands/CustomCommandList",
            Arg.Is<CustomCommandListViewModel>(vm =>
                vm.Commands.Count() == 1 &&
                vm.Commands.First().Id == "hello" &&
                vm.Commands.First().Content == "Hello world!" &&
                vm.Commands.First().Author == "alice"
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldOnlyIncludeCommandsForCurrentRoom_WhenMultipleRoomsExist()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.AddedCommands.AddAsync(new AddedCommand
            {
                Id = "cmd1",
                RoomId = "testroom",
                Content = "content1",
                Author = "user1",
                CreationDate = DateTime.UtcNow
            });
            await setupContext.AddedCommands.AddAsync(new AddedCommand
            {
                Id = "cmd2",
                RoomId = "otherroom",
                Content = "content2",
                Author = "user2",
                CreationDate = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "CustomCommands/CustomCommandList",
            Arg.Is<CustomCommandListViewModel>(vm =>
                vm.Commands.Count() == 1 &&
                vm.Commands.First().Id == "cmd1"
            )
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCultureToViewModel_WhenRenderingTemplate()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        _context.Culture.Returns(culture);

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.AddedCommands.AddAsync(new AddedCommand
            {
                Id = "hello",
                RoomId = "testroom",
                Content = "Bonjour!",
                Author = "alice",
                CreationDate = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<CustomCommandListViewModel>(vm => vm.Culture == culture)
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyHtmlPage_WhenTemplateIsRendered()
    {
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>())
            .Returns("<b>Custom commands</b>");

        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.AddedCommands.AddAsync(new AddedCommand
            {
                Id = "hello",
                RoomId = "testroom",
                Content = "Hello!",
                Author = "alice",
                CreationDate = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        _context.Received(1).ReplyHtmlPage("custom-commands-testroom", "<b>Custom commands</b>");
    }

    [Test]
    public async Task Test_RunAsync_ShouldIncludeAllCommandsForRoom_WhenMultipleCommandsExist()
    {
        using (var setupContext = new BotDbContext(_dbOptions))
        {
            await setupContext.AddedCommands.AddAsync(new AddedCommand
            {
                Id = "cmd1",
                RoomId = "testroom",
                Content = "content1",
                Author = "user1",
                CreationDate = DateTime.UtcNow
            });
            await setupContext.AddedCommands.AddAsync(new AddedCommand
            {
                Id = "cmd2",
                RoomId = "testroom",
                Content = "content2",
                Author = "user2",
                CreationDate = DateTime.UtcNow
            });
            await setupContext.AddedCommands.AddAsync(new AddedCommand
            {
                Id = "cmd3",
                RoomId = "testroom",
                Content = "content3",
                Author = "user3",
                CreationDate = DateTime.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "CustomCommands/CustomCommandList",
            Arg.Is<CustomCommandListViewModel>(vm => vm.Commands.Count() == 3)
        );
    }

    [Test]
    public async Task Test_RunAsync_ShouldPassCancellationToken_WhenProvided()
    {
        var cancellationToken = CancellationToken.None;

        await _command.RunAsync(_context, cancellationToken);

        await _dbContextFactory.Received(1).CreateDbContextAsync(cancellationToken);
    }
}