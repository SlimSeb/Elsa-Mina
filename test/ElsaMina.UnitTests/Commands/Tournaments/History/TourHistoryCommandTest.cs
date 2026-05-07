using System.Globalization;
using ElsaMina.Commands.Tournaments.History;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Rooms;
using ElsaMina.Core.Services.Templates;
using ElsaMina.DataAccess;
using ElsaMina.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Tournaments.History;

public class TourHistoryCommandTest
{
    private IContext _context;
    private ITemplatesManager _templatesManager;
    private IBotDbContextFactory _dbContextFactory;
    private IRoomsManager _roomsManager;
    private TourHistoryCommand _command;
    private DbContextOptions<BotDbContext> _dbOptions;

    private static SavedTournament MakeTournament(string roomId, string format = "gen9ou",
        string winner = "alice", string runnerUp = "bob", string semiFinalists = null,
        int playerCount = 8, DateTimeOffset? endedAt = null) =>
        new()
        {
            RoomId = roomId,
            Format = format,
            Winner = winner,
            RunnerUp = runnerUp,
            SemiFinalists = semiFinalists,
            PlayerCount = playerCount,
            EndedAt = endedAt ?? DateTimeOffset.UtcNow
        };

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IContext>();
        _context.Target.Returns(string.Empty);
        _context.RoomId.Returns("arcade");
        _context.Culture.Returns(CultureInfo.InvariantCulture);
        _context.Room.Returns((IRoom)null);

        _templatesManager = Substitute.For<ITemplatesManager>();
        _templatesManager.GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>()).Returns("<div/>");

        _dbContextFactory = Substitute.For<IBotDbContextFactory>();
        _dbOptions = new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new BotDbContext(_dbOptions)));

        _roomsManager = Substitute.For<IRoomsManager>();
        _roomsManager.GetRoom(Arg.Any<string>()).Returns((IRoom)null);

        _command = new TourHistoryCommand(_dbContextFactory, _templatesManager, _roomsManager);
    }

    [TearDown]
    public async Task TearDown()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.Database.EnsureDeletedAsync();
    }

    [Test]
    public void Test_RequiredRank_ShouldBeRegular()
    {
        Assert.That(_command.RequiredRank, Is.EqualTo(Rank.Regular));
    }

    [Test]
    public void Test_IsAllowedInPrivateMessage_ShouldBeTrue()
    {
        Assert.That(_command.IsAllowedInPrivateMessage, Is.True);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoHistory_WhenNoRecordsExistForRoom()
    {
        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tour_history_no_history", "arcade");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldExcludeRecordsFromOtherRooms()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddAsync(MakeTournament("otherroom"));
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tour_history_no_history", "arcade");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldRenderTemplate_WhenRecordsExist()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddAsync(MakeTournament("arcade"));
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            "Tournaments/History/TourHistory",
            Arg.Is<TourHistoryViewModel>(vm =>
                vm.Entries.Count == 1));
        _context.Received(1).ReplyHtml(Arg.Any<string>(), rankAware: true);
    }

    [Test]
    public async Task Test_RunAsync_ShouldOrderByEndedAtDescending()
    {
        var now = DateTimeOffset.UtcNow;
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddRangeAsync(
            MakeTournament("arcade", winner: "oldest", endedAt: now.AddDays(-2)),
            MakeTournament("arcade", winner: "newest", endedAt: now),
            MakeTournament("arcade", winner: "middle", endedAt: now.AddDays(-1)));
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TourHistoryViewModel>(vm =>
                vm.Entries[0].Winner == "newest" &&
                vm.Entries[1].Winner == "middle" &&
                vm.Entries[2].Winner == "oldest"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldLimitResultsToThirty()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 40; i++)
        {
            await dbContext.SavedTournaments.AddAsync(
                MakeTournament("arcade", endedAt: now.AddMinutes(-i)));
        }
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TourHistoryViewModel>(vm => vm.Entries.Count == 30));
    }

    [Test]
    public async Task Test_RunAsync_ShouldParseSemiFinalists_WhenCommaSeparated()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddAsync(MakeTournament("arcade", semiFinalists: "charlie,dave"));
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TourHistoryViewModel>(vm =>
                vm.Entries[0].SemiFinalists.Count == 2 &&
                vm.Entries[0].SemiFinalists[0] == "charlie" &&
                vm.Entries[0].SemiFinalists[1] == "dave"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReturnEmptySemiFinalists_WhenSemiFinalistsIsNull()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddAsync(MakeTournament("arcade", semiFinalists: null));
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TourHistoryViewModel>(vm =>
                vm.Entries[0].SemiFinalists.Count == 0));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReturnEmptySemiFinalists_WhenSemiFinalistsIsEmpty()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddAsync(MakeTournament("arcade", semiFinalists: ""));
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TourHistoryViewModel>(vm =>
                vm.Entries[0].SemiFinalists.Count == 0));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseRoomNameFromRoomsManager_WhenRoomExists()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddAsync(MakeTournament("arcade"));
        await dbContext.SaveChangesAsync();

        var room = Substitute.For<IRoom>();
        room.Name.Returns("The Arcade");
        _roomsManager.GetRoom("arcade").Returns(room);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TourHistoryViewModel>(vm => vm.Room == "The Arcade"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldFallbackToRoomId_WhenRoomNotInRoomsManager()
    {
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddAsync(MakeTournament("arcade"));
        await dbContext.SaveChangesAsync();

        _roomsManager.GetRoom("arcade").Returns((IRoom)null);

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TourHistoryViewModel>(vm => vm.Room == "arcade"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseContextRoomId_WhenTargetIsEmpty()
    {
        _context.Target.Returns(string.Empty);
        _context.RoomId.Returns("arcade");

        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddAsync(MakeTournament("arcade"));
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TourHistoryViewModel>(vm => vm.Entries.Count == 1));
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseTargetRoomId_WhenTargetIsProvided()
    {
        _context.Target.Returns("OtherRoom");
        _roomsManager.HasRoom("otherroom").Returns(true);

        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddAsync(MakeTournament("otherroom"));
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TourHistoryViewModel>(vm => vm.Entries.Count == 1));
    }

    [Test]
    public async Task Test_RunAsync_ShouldNormalizeTargetRoomId_WhenTargetHasUpperCaseOrSpaces()
    {
        _context.Target.Returns("Other Room");
        _roomsManager.HasRoom("otherroom").Returns(true);

        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddAsync(MakeTournament("otherroom"));
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TourHistoryViewModel>(vm => vm.Entries.Count == 1));
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyRoomNotFound_WhenTargetRoomIsUnknown()
    {
        _context.Target.Returns("unknownroom");
        _roomsManager.HasRoom("unknownroom").Returns(false);

        await _command.RunAsync(_context);

        _context.Received(1).ReplyLocalizedMessage("tour_history_room_not_found", "unknownroom");
        await _templatesManager.DidNotReceive().GetTemplateAsync(Arg.Any<string>(), Arg.Any<object>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseLocalTimeZone_WhenRoomHasNoTimeZone()
    {
        _context.Room.Returns((IRoom)null);

        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddAsync(MakeTournament("arcade"));
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(Arg.Any<string>(), Arg.Any<TourHistoryViewModel>());
    }

    [Test]
    public async Task Test_RunAsync_ShouldUseRoomTimeZone_WhenRoomHasTimeZone()
    {
        var room = Substitute.For<IRoom>();
        room.TimeZone.Returns(TimeZoneInfo.Utc);
        _context.Room.Returns(room);

        var endedAt = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        await using var dbContext = new BotDbContext(_dbOptions);
        await dbContext.SavedTournaments.AddAsync(MakeTournament("arcade", endedAt: endedAt));
        await dbContext.SaveChangesAsync();

        await _command.RunAsync(_context);

        await _templatesManager.Received(1).GetTemplateAsync(
            Arg.Any<string>(),
            Arg.Is<TourHistoryViewModel>(vm =>
                vm.Entries[0].EndedAt.Contains("06/15/2024") &&
                vm.Entries[0].EndedAt.Contains("12:00")));
    }
}
