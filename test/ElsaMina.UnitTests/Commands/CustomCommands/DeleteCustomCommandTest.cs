using ElsaMina.Commands.CustomCommands;
using ElsaMina.Core.Contexts;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.CustomCommands;

public class DeleteCustomCommandTests
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
    public async Task RunAsync_ShouldReplyNotFound_WhenCommandDoesNotExist()
    {
        // Arrange
        var options = CreateOptions();
        await using var setupCtx = new BotDbContext(options);
        await setupCtx.Database.EnsureCreatedAsync();

        var factory = CreateFactoryReturning(setupCtx);
        var context = Substitute.For<IContext>();
        context.Target.Returns("testcommand");
        context.RoomId.Returns("room1");

        var sut = new DeleteCustomCommand(factory);

        // Act
        await sut.RunAsync(context);

        // Assert
        context.Received().ReplyLocalizedMessage("deletecommand_not_found", "testcommand");
    }

    [Test]
    public async Task RunAsync_ShouldDeleteCommandAndReplySuccess_WhenCommandExists()
    {
        // Arrange
        var options = CreateOptions();
        await using (var setupCtx = new BotDbContext(options))
        {
            await setupCtx.Database.EnsureCreatedAsync();
            setupCtx.AddedCommands.Add(new AddedCommand
            {
                Id = "testcommand",
                RoomId = "room1",
                Content = "hello"
            });
            await setupCtx.SaveChangesAsync();
        }

        await using var execCtx = new BotDbContext(options);
        var factory = CreateFactoryReturning(execCtx);

        var context = Substitute.For<IContext>();
        context.Target.Returns("testcommand");
        context.RoomId.Returns("room1");

        var sut = new DeleteCustomCommand(factory);

        // Act
        await sut.RunAsync(context);

        // Assert
        await using (var assertCtx = new BotDbContext(options))
        {
            var deleted = await assertCtx.AddedCommands.FindAsync("testcommand", "room1");
            Assert.IsNull(deleted);
        }

        context.Received().ReplyLocalizedMessage("deletecommand_success", "testcommand");
    }

    [Test]
    public async Task RunAsync_ShouldReplyFailure_WhenExceptionThrown()
    {
        // Arrange
        var factory = Substitute.For<IBotDbContextFactory>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns<Task<BotDbContext>>(x => throw new Exception("boom"));

        var context = Substitute.For<IContext>();
        context.Target.Returns("testcommand");
        context.RoomId.Returns("room1");

        var sut = new DeleteCustomCommand(factory);

        // Act
        await sut.RunAsync(context);

        // Assert
        context.Received().ReplyLocalizedMessage("deletecommand_failure", "boom");
    }
}