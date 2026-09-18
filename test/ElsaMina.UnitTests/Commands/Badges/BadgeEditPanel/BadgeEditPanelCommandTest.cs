using System.Globalization;
using ElsaMina.Commands.Badges.BadgeEditPanel;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Badges.BadgeEditPanel;

public class BadgeEditPanelCommandTest
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

    private static async Task SeedBadges(BotDbContext ctx, string roomId, int count)
    {
        await ctx.Database.EnsureCreatedAsync();
        for (var i = 1; i <= count; i++)
        {
            ctx.Badges.Add(new Badge
            {
                Id = $"badge{i:D2}",
                RoomId = roomId,
                Name = $"Badge {i:D2}",
                Image = $"badge{i:D2}.png"
            });
        }
        await ctx.SaveChangesAsync();
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoBadges_WhenRoomHasNone()
    {
        var options = CreateOptions();
        await using var ctx = new BotDbContext(options);
        await ctx.Database.EnsureCreatedAsync();

        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa");
        configuration.Trigger.Returns("!");

        var room = Substitute.For<IRoom>();
        room.Name.Returns("Cool Room");

        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.GetRoom("room1").Returns(room);

        var context = Substitute.For<IContext>();
        context.Target.Returns("room1");
        context.RoomId.Returns("room1");
        context.IsPrivateMessage.Returns(true);

        var command = new BadgeEditPanelCommand(factory, templatesManager, configuration, roomsManager);

        await command.RunAsync(context);

        context.Received().ReplyLocalizedMessage("badge_edit_panel_no_badges", "Cool Room");
        await templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderEditPanel_WhenBadgesExist()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await SeedBadges(setup, "room1", 2);
        }

        await using var execCtx = new BotDbContext(options);
        var factory = CreateFactoryReturning(execCtx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa");
        configuration.Trigger.Returns("!");

        var room = Substitute.For<IRoom>();
        room.Name.Returns("Cool Room");
        room.Culture.Returns(CultureInfo.InvariantCulture);

        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.GetRoom("room1").Returns(room);

        var context = Substitute.For<IContext>();
        context.Target.Returns("room1");
        context.RoomId.Returns("room1");
        context.IsPrivateMessage.Returns(true);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        templatesManager.GetTemplateAsync("Badges/BadgeEditPanel/BadgeEditPanel", Arg.Any<BadgeEditPanelViewModel>())
            .Returns(Task.FromResult("<div>panel</div>"));

        var command = new BadgeEditPanelCommand(factory, templatesManager, configuration, roomsManager);

        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Badges/BadgeEditPanel/BadgeEditPanel",
            Arg.Is<BadgeEditPanelViewModel>(vm =>
                vm.RoomId == "room1" &&
                vm.RoomName == "Cool Room" &&
                vm.BotName == "Elsa" &&
                vm.Trigger == "!" &&
                vm.EditCommand == "/w Elsa,!editbadge {badgeId}, {name}, {image}, {trophy}, {teamtournament}, room1" &&
                vm.Badges.Count == 2 &&
                vm.Page == 1 &&
                vm.TotalPages == 1
            ));

        context.Received(1).ReplyHtmlPage("badge-edit-room1", "<div>panel</div>");
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderFirstPage_WhenMoreBadgesThanPageSize()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await SeedBadges(setup, "room1", 12);
        }

        await using var execCtx = new BotDbContext(options);
        var factory = CreateFactoryReturning(execCtx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa");
        configuration.Trigger.Returns("!");

        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.GetRoom("room1").Returns((IRoom)null);

        var context = Substitute.For<IContext>();
        context.Target.Returns("room1");
        context.RoomId.Returns("room1");
        context.IsPrivateMessage.Returns(false);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<BadgeEditPanelViewModel>())
            .Returns(Task.FromResult("<div>panel</div>"));

        var command = new BadgeEditPanelCommand(factory, templatesManager, configuration, roomsManager);

        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Badges/BadgeEditPanel/BadgeEditPanel",
            Arg.Is<BadgeEditPanelViewModel>(vm =>
                vm.Page == 1 &&
                vm.TotalPages == 2 &&
                vm.Badges.Count == 10
            ));
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderRequestedPage_WhenPageSpecifiedInTarget()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await SeedBadges(setup, "room1", 12);
        }

        await using var execCtx = new BotDbContext(options);
        var factory = CreateFactoryReturning(execCtx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa");
        configuration.Trigger.Returns("!");

        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.GetRoom("room1").Returns((IRoom)null);

        var context = Substitute.For<IContext>();
        context.Target.Returns("room1, 2");
        context.RoomId.Returns("room1");
        context.IsPrivateMessage.Returns(false);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<BadgeEditPanelViewModel>())
            .Returns(Task.FromResult("<div>panel</div>"));

        var command = new BadgeEditPanelCommand(factory, templatesManager, configuration, roomsManager);

        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Badges/BadgeEditPanel/BadgeEditPanel",
            Arg.Is<BadgeEditPanelViewModel>(vm =>
                vm.Page == 2 &&
                vm.TotalPages == 2 &&
                vm.Badges.Count == 2
            ));
    }

    [Test]
    public async Task Test_RunAsync_ShouldClampToLastPage_WhenPageExceedsTotalPages()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await SeedBadges(setup, "room1", 12);
        }

        await using var execCtx = new BotDbContext(options);
        var factory = CreateFactoryReturning(execCtx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa");
        configuration.Trigger.Returns("!");

        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.GetRoom("room1").Returns((IRoom)null);

        var context = Substitute.For<IContext>();
        context.Target.Returns("room1, 99");
        context.RoomId.Returns("room1");
        context.IsPrivateMessage.Returns(false);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<BadgeEditPanelViewModel>())
            .Returns(Task.FromResult("<div>panel</div>"));

        var command = new BadgeEditPanelCommand(factory, templatesManager, configuration, roomsManager);

        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Badges/BadgeEditPanel/BadgeEditPanel",
            Arg.Is<BadgeEditPanelViewModel>(vm =>
                vm.Page == 2 &&
                vm.TotalPages == 2 &&
                vm.Badges.Count == 2
            ));
    }

    [Test]
    public async Task Test_RunAsync_ShouldDefaultToPage1_WhenPageIsZeroOrNegative()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await SeedBadges(setup, "room1", 12);
        }

        await using var execCtx = new BotDbContext(options);
        var factory = CreateFactoryReturning(execCtx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa");
        configuration.Trigger.Returns("!");

        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.GetRoom("room1").Returns((IRoom)null);

        var context = Substitute.For<IContext>();
        context.Target.Returns("room1, 0");
        context.RoomId.Returns("room1");
        context.IsPrivateMessage.Returns(false);
        context.Culture.Returns(CultureInfo.InvariantCulture);

        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<BadgeEditPanelViewModel>())
            .Returns(Task.FromResult("<div>panel</div>"));

        var command = new BadgeEditPanelCommand(factory, templatesManager, configuration, roomsManager);

        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Badges/BadgeEditPanel/BadgeEditPanel",
            Arg.Is<BadgeEditPanelViewModel>(vm =>
                vm.Page == 1 &&
                vm.TotalPages == 2 &&
                vm.Badges.Count == 10
            ));
    }
}
