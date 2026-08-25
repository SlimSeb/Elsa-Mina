using System.Globalization;
using ElsaMina.Commands.Dolls;
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

public class EditProfilePanelServiceTest
{
    private const string TEMPLATE_KEY = "Profile/EditProfilePanel/EditProfilePanel";

    private DbContextOptions<BotDbContext> _options;
    private BotDbContext _dbContext;
    private IBotDbContextFactory _dbContextFactory;
    private ITemplatesManager _templatesManager;
    private IConfiguration _configuration;
    private IRoomsManager _roomsManager;
    private IDollService _dollService;
    private IContext _context;
    private EditProfilePanelService _sut;

    [SetUp]
    public void SetUp()
    {
        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new BotDbContext(_options);

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>()).Returns(_dbContext);

        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<EditProfilePanelViewModel>())
            .Returns("<div>panel</div>");

        _configuration = Substitute.For<IConfiguration>();
        _configuration.Name.Returns("Elsa");
        _configuration.Trigger.Returns("-");

        _roomsManager = Substitute.For<IRoomsManager>();
        _roomsManager.GetRoom(Arg.Any<string>()).Returns((IRoom)null);

        _dollService = Substitute.For<IDollService>();
        _dollService.ResolveDollsAsync(Arg.Any<IEnumerable<DollHolding>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var sender = Substitute.For<IUser>();
        sender.UserId.Returns("alice");

        _context = Substitute.For<IContext>();
        _context.RoomId.Returns("testroom");
        _context.IsPrivateMessage.Returns(false);
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _context.Sender.Returns(sender);

        _sut = new EditProfilePanelService(_dbContextFactory, _templatesManager, _configuration, _roomsManager,
            _dollService);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    private async Task AddRoomUserAsync(RoomUser roomUser)
    {
        _dbContext.Users.Add(new SavedUser { UserId = roomUser.Id, UserName = roomUser.Id });
        _dbContext.RoomUsers.Add(roomUser);
        await _dbContext.SaveChangesAsync();
    }

    [Test]
    public async Task Test_SendPanelAsync_ShouldRenderThePanel_WhenTheUserHasNoStoredData()
    {
        // Act
        await _sut.SendPanelAsync(_context, "testroom");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(TEMPLATE_KEY,
            Arg.Is<EditProfilePanelViewModel>(viewModel =>
                viewModel.RoomId == "testroom" &&
                viewModel.UserId == "alice" &&
                viewModel.BotName == "Elsa" &&
                viewModel.Trigger == "-" &&
                string.IsNullOrEmpty(viewModel.CurrentEmoji) &&
                viewModel.Dolls.Count == 0));
        _context.Received(1).ReplyHtmlPage("edit-profile-alice", "<div>panel</div>");
    }

    [Test]
    public async Task Test_SendPanelAsync_ShouldRenderTheStoredEmojiAndColor()
    {
        // Arrange
        await AddRoomUserAsync(new RoomUser
        {
            Id = "alice",
            RoomId = "testroom",
            ProfileEmoji = "🎮",
            ProfileBackgroundColor = "#8867aa73",
            ProfileTextColor = "#e0d060",
            PlayTime = TimeSpan.Zero
        });

        // Act
        await _sut.SendPanelAsync(_context, "testroom");

        // Assert
        await _templatesManager.Received(1).GetTemplateAsync(TEMPLATE_KEY,
            Arg.Is<EditProfilePanelViewModel>(viewModel =>
                viewModel.CurrentEmoji == "🎮" &&
                viewModel.CurrentBackgroundColor == "#8867aa73" &&
                viewModel.CurrentTextColor == "#e0d060"));
    }

    [Test]
    public async Task Test_SendPanelAsync_ShouldRenderTheDolls_InTheirShelfOrder()
    {
        // Arrange
        await AddRoomUserAsync(new RoomUser
        {
            Id = "alice",
            RoomId = "testroom",
            PlayTime = TimeSpan.Zero,
            Dolls =
            [
                new DollHolding { DollId = "pikachu", RoomId = "testroom", UserId = "alice", Position = 2 },
                new DollHolding { DollId = "snorlax", RoomId = "testroom", UserId = "alice", Position = 1 }
            ]
        });
        var shelf = new List<Doll>
        {
            new() { Id = "snorlax", Name = "Snorlax", Size = 32, Image = "https://images/snorlax.png" },
            new() { Id = "pikachu", Name = "Pikachu", Size = 16, Image = "https://images/pikachu.png" }
        };
        _dollService.ResolveDollsAsync(Arg.Any<IEnumerable<DollHolding>>(), Arg.Any<CancellationToken>())
            .Returns(shelf);

        // Act
        await _sut.SendPanelAsync(_context, "testroom");

        // Assert
        await _dollService.Received(1).ResolveDollsAsync(
            Arg.Is<IEnumerable<DollHolding>>(holdings => holdings.Select(holding => holding.DollId)
                .OrderBy(dollId => dollId)
                .SequenceEqual(new[] { "pikachu", "snorlax" })),
            Arg.Any<CancellationToken>());
        await _templatesManager.Received(1).GetTemplateAsync(TEMPLATE_KEY,
            Arg.Is<EditProfilePanelViewModel>(viewModel => viewModel.Dolls == shelf));
    }

    [Test]
    public async Task Test_SendPanelAsync_ShouldNotRenderTheDollsOwnedInAnotherRoom()
    {
        // Arrange
        await AddRoomUserAsync(new RoomUser
        {
            Id = "alice",
            RoomId = "otherroom",
            PlayTime = TimeSpan.Zero,
            Dolls = [new DollHolding { DollId = "pikachu", RoomId = "otherroom", UserId = "alice" }]
        });

        // Act
        await _sut.SendPanelAsync(_context, "testroom");

        // Assert
        await _dollService.Received(1).ResolveDollsAsync(
            Arg.Is<IEnumerable<DollHolding>>(holdings => !holdings.Any()),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Test_SendPanelAsync_ShouldUseTheRoomCulture_WhenCalledFromAPrivateMessage()
    {
        // Arrange
        var room = Substitute.For<IRoom>();
        room.Culture.Returns(new CultureInfo("fr-FR"));
        _roomsManager.GetRoom("testroom").Returns(room);
        _context.IsPrivateMessage.Returns(true);

        // Act
        await _sut.SendPanelAsync(_context, "testroom");

        // Assert
        _context.Received().Culture = new CultureInfo("fr-FR");
    }
}
