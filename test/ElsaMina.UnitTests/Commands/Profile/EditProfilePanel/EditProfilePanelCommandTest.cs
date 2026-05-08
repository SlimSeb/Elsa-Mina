using System.Globalization;
using ElsaMina.Commands.Profile.EditProfilePanel;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Profile.EditProfilePanel;

public class EditProfilePanelCommandTest
{
    private DbContextOptions<BotDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private IBotDbContextFactory CreateFactoryReturning(BotDbContext ctx)
    {
        var factory = Substitute.For<IBotDbContextFactory>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(ctx);
        return factory;
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderPanel_WithNoCurrentEmoji_WhenUserHasNoStoredData()
    {
        var options = CreateOptions();
        await using var ctx = new BotDbContext(options);
        await ctx.Database.EnsureCreatedAsync();

        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa");
        configuration.Trigger.Returns("-");
        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.GetRoom("testroom").Returns((IRoom)null);

        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("alice");

        var context = Substitute.For<IContext>();
        context.Target.Returns("testroom");
        context.RoomId.Returns("testroom");
        context.IsPrivateMessage.Returns(false);
        context.Culture.Returns(CultureInfo.InvariantCulture);
        context.Sender.Returns(sender);

        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<EditProfilePanelViewModel>())
            .Returns(Task.FromResult("<div>panel</div>"));

        var command = new EditProfilePanelCommand(factory, templatesManager, configuration, roomsManager);

        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Profile/EditProfilePanel/EditProfilePanel",
            Arg.Is<EditProfilePanelViewModel>(vm =>
                vm.RoomId == "testroom" &&
                vm.UserId == "alice" &&
                vm.BotName == "Elsa" &&
                vm.Trigger == "-" &&
                string.IsNullOrEmpty(vm.CurrentEmoji)
            ));

        context.Received(1).ReplyHtmlPage("edit-profile-alice", "<div>panel</div>");
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderPanel_WithCurrentEmoji_WhenUserHasStoredEmoji()
    {
        var options = CreateOptions();
        await using (var setup = new BotDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Users.Add(new SavedUser { UserId = "bob", UserName = "Bob" });
            setup.RoomUsers.Add(new RoomUser
            {
                Id = "bob",
                RoomId = "testroom",
                ProfileEmoji = "🎮",
                PlayTime = TimeSpan.Zero
            });
            await setup.SaveChangesAsync();
        }

        await using var ctx = new BotDbContext(options);
        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa");
        configuration.Trigger.Returns("-");
        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.GetRoom("testroom").Returns((IRoom)null);

        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("bob");

        var context = Substitute.For<IContext>();
        context.Target.Returns(string.Empty);
        context.RoomId.Returns("testroom");
        context.IsPrivateMessage.Returns(false);
        context.Culture.Returns(CultureInfo.InvariantCulture);
        context.Sender.Returns(sender);

        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<EditProfilePanelViewModel>())
            .Returns(Task.FromResult("<div>panel</div>"));

        var command = new EditProfilePanelCommand(factory, templatesManager, configuration, roomsManager);

        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Profile/EditProfilePanel/EditProfilePanel",
            Arg.Is<EditProfilePanelViewModel>(vm =>
                vm.CurrentEmoji == "🎮"
            ));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseContextRoomId_WhenTargetIsEmpty()
    {
        var options = CreateOptions();
        await using var ctx = new BotDbContext(options);
        await ctx.Database.EnsureCreatedAsync();

        var factory = CreateFactoryReturning(ctx);
        var templatesManager = Substitute.For<ITemplatesManager>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.Name.Returns("Elsa");
        configuration.Trigger.Returns("-");
        var roomsManager = Substitute.For<IRoomsManager>();
        roomsManager.GetRoom("defaultroom").Returns((IRoom)null);

        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("alice");

        var context = Substitute.For<IContext>();
        context.Target.Returns(string.Empty);
        context.RoomId.Returns("defaultroom");
        context.IsPrivateMessage.Returns(false);
        context.Culture.Returns(CultureInfo.InvariantCulture);
        context.Sender.Returns(sender);

        templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<EditProfilePanelViewModel>())
            .Returns(Task.FromResult("<div>panel</div>"));

        var command = new EditProfilePanelCommand(factory, templatesManager, configuration, roomsManager);

        await command.RunAsync(context);

        await templatesManager.Received(1).GetTemplateAsync(
            "Profile/EditProfilePanel/EditProfilePanel",
            Arg.Is<EditProfilePanelViewModel>(vm => vm.RoomId == "defaultroom"));
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        var factory = Substitute.For<IBotDbContextFactory>();
        var templatesManager = Substitute.For<ITemplatesManager>();
        var configuration = Substitute.For<IConfiguration>();
        var roomsManager = Substitute.For<IRoomsManager>();

        var command = new EditProfilePanelCommand(factory, templatesManager, configuration, roomsManager);

        Assert.That(command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        var factory = Substitute.For<IBotDbContextFactory>();
        var templatesManager = Substitute.For<ITemplatesManager>();
        var configuration = Substitute.For<IConfiguration>();
        var roomsManager = Substitute.For<IRoomsManager>();

        var command = new EditProfilePanelCommand(factory, templatesManager, configuration, roomsManager);

        Assert.That(command.IsAllowedInPrivateMessage, Is.True);
    }
}
