using ElsaMina.Commands.Polls;
using ElsaMina.Core.Services.Clock;
using ElsaMina.DataAccess;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Polls;

public class PollEndHandlerTests
{
    private PollEndHandler _sut;
    private IClockService _clockService;
    private IBotDbContextFactory _dbContextFactory;

    private DbContextOptions<BotDbContext> _dbContextOptions;
    private readonly DateTimeOffset _expectedTime = new DateTime(2025, 10, 27, 10, 0, 0, DateTimeKind.Utc);
    private const string TestRoomId = "battleformat";

    [SetUp]
    public void Setup()
    {
        // Arrange
        _dbContextOptions = CreateOptions();
        _clockService = Substitute.For<IClockService>();
        _clockService.CurrentUtcDateTimeOffset.Returns(_expectedTime);
        _dbContextFactory = CreateFactory(_dbContextOptions);
        _sut = new PollEndHandler(_clockService, _dbContextFactory);
    }

    private DbContextOptions<BotDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<BotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private IBotDbContextFactory CreateFactory(DbContextOptions<BotDbContext> options)
    {
        var factory = Substitute.For<IBotDbContextFactory>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new BotDbContext(options)));
        return factory;
    }

    [Test]
    public async Task HandleReceivedMessageAsync_WhenEnglishPollEnds_ShouldSavePollToDatabase()
    {
        // Arrange
        var parts = new[] { "", "html", "<div>Poll ended: A new poll result.</div>" };
        var expectedContent = parts[2];

        // Act
        await _sut.HandleReceivedMessageAsync(parts, TestRoomId);

        // Assert
        await using var assertContext = new BotDbContext(_dbContextOptions);
        var savedPolls = await assertContext.SavedPolls.ToListAsync();

        Assert.That(savedPolls, Is.Not.Empty);
        var savedPoll = savedPolls.First();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(savedPoll.RoomId, Is.EqualTo(TestRoomId));
            Assert.That(savedPoll.Content, Is.EqualTo(expectedContent));
            Assert.That(savedPoll.EndedAt, Is.EqualTo(_expectedTime));
        }
    }

    [Test]
    public async Task HandleReceivedMessageAsync_WhenFrenchPollEnds_ShouldSavePollToDatabase()
    {
        // Arrange
        var parts = new[] { "", "html", "<span>Sondage terminé! Le resultat est...</span>" };
        var expectedContent = parts[2];

        // Act
        await _sut.HandleReceivedMessageAsync(parts, TestRoomId);

        // Assert
        await using var assertContext = new BotDbContext(_dbContextOptions);
        var savedPolls = await assertContext.SavedPolls.ToListAsync();

        Assert.That(savedPolls, Is.Not.Empty);
        var savedPoll = savedPolls.First();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(savedPoll.RoomId, Is.EqualTo(TestRoomId));
            Assert.That(savedPoll.Content, Is.EqualTo(expectedContent));
            Assert.That(savedPoll.EndedAt, Is.EqualTo(_expectedTime));
        }
    }

    [Test]
    public async Task HandleReceivedMessageAsync_WhenMessageIsNotHtml_ShouldNotSavePoll()
    {
        // Arrange
        var parts = new[] { "", "text", "Poll ended: A new poll result." };

        // Act
        await _sut.HandleReceivedMessageAsync(parts, TestRoomId);

        // Assert
        await using var assertContext = new BotDbContext(_dbContextOptions);
        var savedPolls = await assertContext.SavedPolls.ToListAsync();

        Assert.That(savedPolls, Is.Empty);
    }

    [Test]
    public async Task HandleReceivedMessageAsync_WhenHtmlDoesNotIndicateEnd_ShouldNotSavePoll()
    {
        // Arrange
        var parts = new[] { "", "html", "<div>A new poll has started.</div>" };

        // Act
        await _sut.HandleReceivedMessageAsync(parts, TestRoomId);

        // Assert
        await using var assertContext = new BotDbContext(_dbContextOptions);
        var savedPolls = await assertContext.SavedPolls.ToListAsync();

        Assert.That(savedPolls, Is.Empty);
    }

    [Test]
    public async Task HandleReceivedMessageAsync_WhenPartsLengthIsLessThanThree_ShouldNotSavePoll()
    {
        // Arrange
        var parts = new[] { "", "html" };

        // Act
        await _sut.HandleReceivedMessageAsync(parts, TestRoomId);

        // Assert
        await using var assertContext = new BotDbContext(_dbContextOptions);
        var savedPolls = await assertContext.SavedPolls.ToListAsync();

        Assert.That(savedPolls, Is.Empty);
    }
}