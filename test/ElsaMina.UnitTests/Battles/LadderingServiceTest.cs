using ElsaMina.Battles;
using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.System;
using NSubstitute;

namespace ElsaMina.UnitTests.Battles;

[TestFixture]
public class LadderingServiceTest
{
    private const string FORMAT = "gen9randombattle";
    private const string SEARCH = "/search gen9randombattle";
    private static readonly DateTimeOffset START = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IBot _bot;
    private IClockService _clockService;
    private ISystemService _systemService;
    private LadderingService _ladderingService;
    private DateTimeOffset _now;
    private readonly List<(TimeSpan Delay, TaskCompletionSource Completion)> _sleeps = [];

    [SetUp]
    public void SetUp()
    {
        _bot = Substitute.For<IBot>();
        _clockService = Substitute.For<IClockService>();
        _systemService = Substitute.For<ISystemService>();
        _now = START;
        _sleeps.Clear();
        _clockService.CurrentUtcDateTimeOffset.Returns(_ => _now);
        _systemService.SleepAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(callInfo =>
        {
            var completion = new TaskCompletionSource();
            callInfo.Arg<CancellationToken>().Register(() => completion.TrySetCanceled());
            _sleeps.Add((callInfo.Arg<TimeSpan>(), completion));
            return completion.Task;
        });
        _ladderingService = new LadderingService(_bot, Substitute.For<IBattleTeamsService>(), _clockService,
            _systemService);
    }

    // Completed from the thread pool, where the service's continuation runs inline before Task.Run ends
    private async Task ElapseLastSleepAsync()
    {
        var (delay, completion) = _sleeps[^1];
        _now += delay;
        await Task.Run(() => completion.TrySetResult());
    }

    [Test]
    public void Test_Start_ShouldSearchFormat()
    {
        // Act
        _ladderingService.Start(FORMAT);

        // Assert
        _bot.Received(1).Say(string.Empty, SEARCH);
    }

    [Test]
    public void Test_OnBattleEnded_ShouldSearchAgain_WhenLaddering()
    {
        // Arrange
        _ladderingService.Start(FORMAT);
        _ladderingService.OnSearchStateUpdated(false, true);

        // Act
        _ladderingService.OnBattleEnded();

        // Assert
        _bot.Received(2).Say(string.Empty, SEARCH);
    }

    [Test]
    public void Test_OnBattleEnded_ShouldNotSearch_WhenNotLaddering()
    {
        // Act
        _ladderingService.OnBattleEnded();

        // Assert
        _bot.DidNotReceiveWithAnyArgs().Say(default, default);
    }

    [Test]
    public void Test_OnSearchStateUpdated_ShouldSearch_WhenNeitherSearchingNorInGame()
    {
        // Arrange
        _ladderingService.Start(FORMAT);
        _ladderingService.OnSearchStateUpdated(false, true);

        // Act
        _ladderingService.OnSearchStateUpdated(false, false);

        // Assert
        _bot.Received(2).Say(string.Empty, SEARCH);
    }

    [Test]
    public void Test_OnBattleEnded_ShouldNotSearchTwice_WhenSearchStateUpdateAlreadySearched()
    {
        // Arrange
        _ladderingService.Start(FORMAT);
        _ladderingService.OnSearchStateUpdated(false, true);
        _ladderingService.OnSearchStateUpdated(false, false);

        // Act
        _ladderingService.OnBattleEnded();

        // Assert
        _bot.Received(2).Say(string.Empty, SEARCH);
    }

    [Test]
    public void Test_OnSearchStateUpdated_ShouldNotSearchAgain_WhenSearchAwaitsConfirmation()
    {
        // Arrange
        _ladderingService.Start(FORMAT);

        // Act
        _ladderingService.OnSearchStateUpdated(false, false);

        // Assert
        _bot.Received(1).Say(string.Empty, SEARCH);
    }

    [Test]
    public void Test_OnSearchStateUpdated_ShouldIgnoreUpdate_WhenNotLaddering()
    {
        // Act
        _ladderingService.OnSearchStateUpdated(false, false);

        // Assert
        _bot.DidNotReceiveWithAnyArgs().Say(default, default);
    }

    [Test]
    public async Task Test_Search_ShouldRetryAfterDelay_WhenServerDoesNotConfirmIt()
    {
        // Arrange
        _ladderingService.Start(FORMAT);

        // Act
        await ElapseLastSleepAsync();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_sleeps[0].Delay, Is.EqualTo(LadderingService.SEARCH_CONFIRMATION_TIMEOUT));
            Assert.That(_sleeps[^1].Delay, Is.EqualTo(LadderingService.FIRST_RETRY_DELAY));
        }

        _bot.Received(1).Say(string.Empty, SEARCH);

        // Act
        await ElapseLastSleepAsync();

        // Assert
        _bot.Received(2).Say(string.Empty, SEARCH);
    }

    [Test]
    public async Task Test_Search_ShouldWaitLongerBetweenRetries_WhenSearchesKeepFailing()
    {
        // Arrange
        _ladderingService.Start(FORMAT);
        var retryDelays = new List<TimeSpan>();

        // Act
        for (var i = 0; i < 5; i++)
        {
            await ElapseLastSleepAsync();
            retryDelays.Add(_sleeps[^1].Delay);
            await ElapseLastSleepAsync();
        }

        // Assert
        Assert.That(retryDelays, Is.EqualTo(new[]
        {
            TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(3),
            TimeSpan.FromMinutes(3)
        }));
        _bot.Received(6).Say(string.Empty, SEARCH);
    }

    [Test]
    public async Task Test_Search_ShouldResetRetryDelay_WhenSearchIsConfirmed()
    {
        // Arrange
        _ladderingService.Start(FORMAT);
        await ElapseLastSleepAsync();
        await ElapseLastSleepAsync();
        await ElapseLastSleepAsync();
        await ElapseLastSleepAsync();
        _ladderingService.OnSearchStateUpdated(true, false);
        _ladderingService.OnSearchStateUpdated(false, false);

        // Act
        await ElapseLastSleepAsync();

        // Assert
        Assert.That(_sleeps[^1].Delay, Is.EqualTo(LadderingService.FIRST_RETRY_DELAY));
    }

    [Test]
    public async Task Test_OnSearchStateUpdated_ShouldCancelRetry_WhenSearchIsConfirmed()
    {
        // Arrange
        _ladderingService.Start(FORMAT);

        // Act
        _ladderingService.OnSearchStateUpdated(true, false);
        await ElapseLastSleepAsync();

        // Assert
        Assert.That(_sleeps, Has.Count.EqualTo(1));
        _bot.Received(1).Say(string.Empty, SEARCH);
    }

    [Test]
    public async Task Test_Stop_ShouldCancelRetry()
    {
        // Arrange
        _ladderingService.Start(FORMAT);
        await ElapseLastSleepAsync();

        // Act
        _ladderingService.Stop();
        await ElapseLastSleepAsync();

        // Assert
        Assert.That(_ladderingService.IsLaddering, Is.False);
        _bot.Received(1).Say(string.Empty, SEARCH);
    }

    [Test]
    public async Task Test_Search_ShouldWaitForRateLimitWindow_WhenTooManySearchesWereSent()
    {
        // Arrange
        _ladderingService.Start(FORMAT);
        for (var i = 1; i < LadderingService.MAX_SEARCHES_PER_WINDOW; i++)
        {
            _ladderingService.OnSearchStateUpdated(false, true);
            _now += TimeSpan.FromSeconds(10);
            _ladderingService.OnSearchStateUpdated(false, false);
        }

        _ladderingService.OnSearchStateUpdated(false, true);
        _now += TimeSpan.FromSeconds(10);

        // Act
        _ladderingService.OnBattleEnded();

        // Assert
        var expectedDelay = LadderingService.SEARCH_WINDOW - TimeSpan.FromSeconds(10 * LadderingService.MAX_SEARCHES_PER_WINDOW);
        Assert.That(_sleeps[^1].Delay, Is.EqualTo(expectedDelay));
        _bot.Received(LadderingService.MAX_SEARCHES_PER_WINDOW).Say(string.Empty, SEARCH);

        // Act
        await ElapseLastSleepAsync();

        // Assert
        _bot.Received(LadderingService.MAX_SEARCHES_PER_WINDOW + 1).Say(string.Empty, SEARCH);
    }

    [Test]
    public void Test_Search_ShouldNotWait_WhenOldSearchesLeftRateLimitWindow()
    {
        // Arrange
        _ladderingService.Start(FORMAT);
        for (var i = 1; i < LadderingService.MAX_SEARCHES_PER_WINDOW; i++)
        {
            _ladderingService.OnSearchStateUpdated(false, true);
            _ladderingService.OnSearchStateUpdated(false, false);
        }

        _ladderingService.OnSearchStateUpdated(false, true);
        _now += LadderingService.SEARCH_WINDOW;

        // Act
        _ladderingService.OnBattleEnded();

        // Assert
        _bot.Received(LadderingService.MAX_SEARCHES_PER_WINDOW + 1).Say(string.Empty, SEARCH);
    }

    [Test]
    public void Test_Status_ShouldTellFormatAndStart_WhenLaddering()
    {
        // Arrange
        _ladderingService.Start(FORMAT);
        _now += TimeSpan.FromMinutes(5);

        // Act
        _ladderingService.Start(FORMAT);
        var status = _ladderingService.Status;

        // Assert: starting the same format again keeps when it started
        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.IsLaddering, Is.True);
            Assert.That(status.Format, Is.EqualTo(FORMAT));
            Assert.That(status.Since, Is.EqualTo(START));
            Assert.That(status.IsWaitingForConfirmation, Is.True);
        }
    }

    [Test]
    public void Test_Status_ShouldBeOff_WhenStopped()
    {
        // Arrange
        _ladderingService.Start(FORMAT);

        // Act
        _ladderingService.Stop();
        var status = _ladderingService.Status;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.IsLaddering, Is.False);
            Assert.That(status.Since, Is.Null);
            Assert.That(status.NextSearchAt, Is.Null);
        }
    }

    [Test]
    public void Test_Status_ShouldTellServerSearchState_WhenUpdated()
    {
        // Arrange
        _ladderingService.Start(FORMAT);

        // Act
        _ladderingService.OnSearchStateUpdated(false, true);
        var status = _ladderingService.Status;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.IsInGame, Is.True);
            Assert.That(status.IsSearching, Is.False);
            Assert.That(status.IsWaitingForConfirmation, Is.False);
        }
    }

    [Test]
    public async Task Test_Status_ShouldTellNextSearch_WhenRetrying()
    {
        // Arrange
        _ladderingService.Start(FORMAT);

        // Act
        await ElapseLastSleepAsync();
        var status = _ladderingService.Status;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(status.NextSearchAt, Is.EqualTo(_now + LadderingService.FIRST_RETRY_DELAY));
            Assert.That(status.FailedSearchCount, Is.EqualTo(1));
            Assert.That(status.IsWaitingForConfirmation, Is.False);
        }

        // Act
        await ElapseLastSleepAsync();

        // Assert: the retry was sent
        Assert.That(_ladderingService.Status.NextSearchAt, Is.Null);
    }
}
