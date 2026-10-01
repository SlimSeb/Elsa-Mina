using ElsaMina.Core.Services.Lifecycle;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Lifecycle;

public class BotLifecycleServiceTest
{
    private IBotLifecycleParticipant _firstParticipant;
    private IBotLifecycleParticipant _secondParticipant;
    private BotLifecycleService _service;

    [SetUp]
    public void SetUp()
    {
        _firstParticipant = Substitute.For<IBotLifecycleParticipant>();
        _secondParticipant = Substitute.For<IBotLifecycleParticipant>();
        _service = new BotLifecycleService(
            new Lazy<IEnumerable<IBotLifecycleParticipant>>(() => [_firstParticipant, _secondParticipant]));
    }

    [Test]
    public async Task Test_OnStartingAsync_ShouldStartEveryParticipant()
    {
        // Act
        await _service.OnStartingAsync();

        // Assert
        await _firstParticipant.Received(1).OnStartingAsync(Arg.Any<CancellationToken>());
        await _secondParticipant.Received(1).OnStartingAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void Test_OnStartingAsync_ShouldThrow_WhenAParticipantFailsToStart()
    {
        // Arrange
        _firstParticipant.OnStartingAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(() => _service.OnStartingAsync());
    }

    [Test]
    public async Task Test_OnExitingAsync_ShouldExitOtherParticipants_WhenOneFails()
    {
        // Arrange
        _firstParticipant.OnExitingAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));

        // Act
        await _service.OnExitingAsync();

        // Assert
        await _secondParticipant.Received(1).OnExitingAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public void Test_OnExitingAsync_ShouldNotThrow_WhenAParticipantIsCancelled()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        _firstParticipant.OnExitingAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled(cancellationTokenSource.Token));

        // Act & Assert
        Assert.DoesNotThrowAsync(() => _service.OnExitingAsync(cancellationTokenSource.Token));
    }
}
