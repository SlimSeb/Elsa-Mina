using ElsaMina.Commands.CustomCommands;
using ElsaMina.Core.Contexts;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.CustomCommands;

public class EditCustomCommandTests
{
    private static DbContextOptions<BotDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static IBotDbContextFactory CreateFactoryReturning(BotDbContext ctx)
    {
        var factory = Substitute.For<IBotDbContextFactory>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(ctx);
        return factory;
    }

    [Test]
    public async Task RunAsync_ShouldEditCommandAndReplySuccess_WhenCommandExists()
    {
        // Arrange
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.AddedCommands.Add(new AddedCommand
            {
                Id = "hello",
                RoomId = "room1",
                Content = "old"
            });
            await setup.SaveChangesAsync();
        }

        await using var execCtx = new BotDbContext(options);
        var factory = CreateFactoryReturning(execCtx);

        var context = Substitute.For<IContext>();
        context.Target.Returns("hello, new content");
        context.RoomId.Returns("room1");

        var sut = new EditCustomCommand(factory);

        // Act
        await sut.RunAsync(context);

        // Assert
        await using (var assertCtx = new BotDbContext(options))
        {
            var cmd = await assertCtx.AddedCommands.FindAsync("hello", "room1");
            Assert.That(cmd.Content, Is.EqualTo("new content"));
        }

        context.Received().ReplyLocalizedMessage("editcommand_success", "hello");
    }

    [Test]
    public async Task RunAsync_ShouldReplyHelp_WhenCommandDoesNotExist()
    {
        // Arrange
        var options = CreateOptions();
        await using var ctx = new BotDbContext(options);
        await ctx.Database.EnsureCreatedAsync();

        var factory = CreateFactoryReturning(ctx);

        var context = Substitute.For<IContext>();
        context.Target.Returns("missingcmd, new text");
        context.RoomId.Returns("room1");

        var sut = new EditCustomCommand(factory);

        // Act
        await sut.RunAsync(context);

        // Assert
        context.Received().ReplyLocalizedMessage("editcommand_help");
    }
}