using System.Globalization;
using ElsaMina.Commands.Polls;
using ElsaMina.Commands.Polls.ShowPolls;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Polls;

public class ShowPollsCommandTest
{
    private const string TestRoomId = "currentroom";
    private const string TargetRoomId = "targetroom";

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

    private static async Task SeedPolls(BotDbContext ctx, string roomId, int count)
    {
        await ctx.Database.EnsureCreatedAsync();
        for (var i = 1; i <= count; i++)
        {
            ctx.SavedPolls.Add(new SavedPoll
            {
                RoomId = roomId,
                Content = $"Poll {i:D2}",
                EndedAt = new DateTimeOffset(2025, 1, i, 10, 0, 0, TimeSpan.Zero)
            });
        }
        await ctx.SaveChangesAsync();
    }

    private static ShowPollsCommand CreateCommand(IBotDbContextFactory factory,
        ITemplatesManager templatesManager,
        IRoomsManager roomsManager,
        IConfiguration configuration = null)
    {
        configuration ??= CreateDefaultConfiguration();
        return new ShowPollsCommand(roomsManager, factory, templatesManager, configuration);
    }

    private static IConfiguration CreateDefaultConfiguration()
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa");
        configuration.Trigger.Returns("-");
        return configuration;
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoPollsMessage_WhenCurrentRoomHasNoPolls()
    {
        var options = CreateOptions();
        await using var ctx = new BotDbContext(options);
        await ctx.Database.EnsureCreatedAsync();

        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        var roomsManager = Substitute.For<IRoomsManager>();

        var context = Substitute.For<IContext>();
        context.Target.Returns("");
        context.RoomId.Returns(TestRoomId);

        var command = CreateCommand(factory, templatesManager, roomsManager);
        await command.RunAsync(context);

        context.Received(1).ReplyRankAwareLocalizedMessage("show_polls_no_polls", TestRoomId);
        context.DidNotReceive().ReplyHtmlPage(Arg.Any<string>(), Arg.Any<string>());
        await templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomNotExist_WhenTargetRoomIsUnknown()
    {
        var options = CreateOptions();
        await using var ctx = new BotDbContext(options);
        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.HasRoom(TargetRoomId).Returns(false);

        var context = Substitute.For<IContext>();
        context.Target.Returns(TargetRoomId);

        var command = CreateCommand(factory, templatesManager, roomsManager);
        await command.RunAsync(context);

        context.Received(1).ReplyRankAwareLocalizedMessage("show_polls_room_not_exist", TargetRoomId);
        context.DidNotReceive().ReplyHtmlPage(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplate_WhenCurrentRoomHasPolls()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await SeedPolls(setup, TestRoomId, 2);
        }

        await using var ctx = new BotDbContext(options);
        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<ShowPollsViewModel>())
            .Returns("<div>polls</div>");

        var room = Substitute.For<IRoom>();
        room.Name.Returns("Current Room");
        room.TimeZone.Returns(TimeZoneInfo.Utc);

        var roomsManager = Substitute.For<IRoomsManager>();

        var context = Substitute.For<IContext>();
        context.Target.Returns("");
        context.RoomId.Returns(TestRoomId);
        context.Room.Returns(room);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        var command = CreateCommand(factory, templatesManager, roomsManager);
        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Polls/ShowPolls/ShowPolls",
            Arg.Is<ShowPollsViewModel>(vm =>
                vm.RoomId == TestRoomId &&
                vm.BotName == "Elsa" &&
                vm.Trigger == "-" &&
                vm.Polls.Count == 2 &&
                vm.Page == 1 &&
                vm.TotalPages == 1));
        context.Received(1).ReplyHtmlPage("polls-history", Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplate_WhenTargetRoomHasPolls()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await SeedPolls(setup, TargetRoomId, 1);
        }

        await using var ctx = new BotDbContext(options);
        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<ShowPollsViewModel>())
            .Returns("<div>polls</div>");

        var room = Substitute.For<IRoom>();
        room.Name.Returns("Target Room");
        room.TimeZone.Returns(TimeZoneInfo.Utc);

        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.HasRoom(TargetRoomId).Returns(true);
        roomsManager.GetRoom(TargetRoomId).Returns(room);

        var context = Substitute.For<IContext>();
        context.Target.Returns(TargetRoomId);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        var command = CreateCommand(factory, templatesManager, roomsManager);
        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Polls/ShowPolls/ShowPolls",
            Arg.Is<ShowPollsViewModel>(vm =>
                vm.RoomId == TargetRoomId &&
                vm.RoomName == "Target Room" &&
                vm.Polls.Count == 1));
        context.Received(1).ReplyHtmlPage("polls-history", Arg.Any<string>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderFirstPage_WhenMorePollsThanPageSize()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await SeedPolls(setup, TestRoomId, 12);
        }

        await using var ctx = new BotDbContext(options);
        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<ShowPollsViewModel>())
            .Returns("<div>polls</div>");

        var roomsManager = Substitute.For<IRoomsManager>();

        var context = Substitute.For<IContext>();
        context.Target.Returns("");
        context.RoomId.Returns(TestRoomId);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        var command = CreateCommand(factory, templatesManager, roomsManager);
        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Polls/ShowPolls/ShowPolls",
            Arg.Is<ShowPollsViewModel>(vm =>
                vm.Page == 1 &&
                vm.TotalPages == 2 &&
                vm.Polls.Count == 10));
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderRequestedPage_WhenPageSpecifiedInTarget()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await SeedPolls(setup, TestRoomId, 12);
        }

        await using var ctx = new BotDbContext(options);
        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<ShowPollsViewModel>())
            .Returns("<div>polls</div>");

        var roomsManager = Substitute.For<IRoomsManager>();

        var context = Substitute.For<IContext>();
        context.Target.Returns($"{TestRoomId}, 2");
        context.RoomId.Returns(TestRoomId);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        roomsManager.HasRoom(TestRoomId).Returns(true);

        var command = CreateCommand(factory, templatesManager, roomsManager);
        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Polls/ShowPolls/ShowPolls",
            Arg.Is<ShowPollsViewModel>(vm =>
                vm.Page == 2 &&
                vm.TotalPages == 2 &&
                vm.Polls.Count == 2));
    }

    [Test]
    public async Task Test_RunAsync_ShouldClampToLastPage_WhenPageExceedsTotalPages()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await SeedPolls(setup, TestRoomId, 12);
        }

        await using var ctx = new BotDbContext(options);
        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<ShowPollsViewModel>())
            .Returns("<div>polls</div>");

        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.HasRoom(TestRoomId).Returns(true);

        var context = Substitute.For<IContext>();
        context.Target.Returns($"{TestRoomId}, 99");
        context.RoomId.Returns(TestRoomId);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        var command = CreateCommand(factory, templatesManager, roomsManager);
        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Polls/ShowPolls/ShowPolls",
            Arg.Is<ShowPollsViewModel>(vm =>
                vm.Page == 2 &&
                vm.TotalPages == 2 &&
                vm.Polls.Count == 2));
    }

    [Test]
    public async Task Test_RunAsync_ShouldDefaultToPage1_WhenPageIsZeroOrNegative()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await SeedPolls(setup, TestRoomId, 12);
        }

        await using var ctx = new BotDbContext(options);
        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<ShowPollsViewModel>())
            .Returns("<div>polls</div>");

        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.HasRoom(TestRoomId).Returns(true);

        var context = Substitute.For<IContext>();
        context.Target.Returns($"{TestRoomId}, 0");
        context.RoomId.Returns(TestRoomId);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        var command = CreateCommand(factory, templatesManager, roomsManager);
        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Polls/ShowPolls/ShowPolls",
            Arg.Is<ShowPollsViewModel>(vm =>
                vm.Page == 1 &&
                vm.TotalPages == 2 &&
                vm.Polls.Count == 10));
    }

    [Test]
    public async Task Test_RunAsync_ShouldSetCultureFromRoom_WhenCalledViaPrivateMessage()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await SeedPolls(setup, TargetRoomId, 1);
        }

        await using var ctx = new BotDbContext(options);
        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<ShowPollsViewModel>())
            .Returns("<div>polls</div>");

        var roomCulture = new CultureInfo("fr-FR");
        var room = Substitute.For<IRoom>();
        room.Culture.Returns(roomCulture);
        room.TimeZone.Returns(TimeZoneInfo.Utc);

        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.HasRoom(TargetRoomId).Returns(true);
        roomsManager.GetRoom(TargetRoomId).Returns(room);

        var context = Substitute.For<IContext>();
        context.Target.Returns(TargetRoomId);
        context.IsPrivateMessage.Returns(true);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        var command = CreateCommand(factory, templatesManager, roomsManager);
        await command.RunAsync(context);

        context.Received(1).Culture = roomCulture;
    }
}
