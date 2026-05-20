using ElsaMina.Commands.Showdown.Ladder.EloHistory;

namespace ElsaMina.UnitTests.Commands.Showdown.Ladder.EloHistory;

public class EloProgressionManagerTest
{
    private EloProgressionManager _manager;

    [SetUp]
    public void SetUp()
    {
        _manager = new EloProgressionManager();
    }

    [Test]
    public void Test_GetAllTrackedUsers_ShouldReturnEmpty_WhenNothingTracked()
    {
        // Act
        var result = _manager.GetAllTrackedUsers();

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Test_TrackUser_ShouldReturnTrue_WhenUserIsNew()
    {
        // Act
        var result = _manager.TrackUser("gen9ou", "alice");

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void Test_TrackUser_ShouldReturnFalse_WhenUserAlreadyTracked()
    {
        // Arrange
        _manager.TrackUser("gen9ou", "alice");

        // Act
        var result = _manager.TrackUser("gen9ou", "alice");

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Test_TrackUser_ShouldTreatSameUserInDifferentFormatsAsDistinct()
    {
        // Act
        var first = _manager.TrackUser("gen9ou", "alice");
        var second = _manager.TrackUser("gen8ou", "alice");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            Assert.That(_manager.GetAllTrackedUsers(), Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Test_UntrackUser_ShouldReturnTrue_WhenUserWasTracked()
    {
        // Arrange
        _manager.TrackUser("gen9ou", "alice");

        // Act
        var result = _manager.UntrackUser("gen9ou", "alice");

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void Test_UntrackUser_ShouldReturnFalse_WhenUserWasNotTracked()
    {
        // Act
        var result = _manager.UntrackUser("gen9ou", "alice");

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void Test_UntrackUser_ShouldRemoveUser_FromTrackedSet()
    {
        // Arrange
        _manager.TrackUser("gen9ou", "alice");
        _manager.UntrackUser("gen9ou", "alice");

        // Act
        var result = _manager.GetAllTrackedUsers();

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Test_UntrackUser_ShouldOnlyRemoveMatchingEntry()
    {
        // Arrange
        _manager.TrackUser("gen9ou", "alice");
        _manager.TrackUser("gen8ou", "alice");

        // Act
        _manager.UntrackUser("gen9ou", "alice");

        // Assert
        Assert.That(_manager.GetAllTrackedUsers(),
            Is.EquivalentTo(new[] { new EloTrackedUser("gen8ou", "alice") }));
    }

    [Test]
    public void Test_GetAllTrackedUsers_ShouldReturnAllTrackedUsers()
    {
        // Arrange
        _manager.TrackUser("gen9ou", "alice");
        _manager.TrackUser("gen9ou", "bob");
        _manager.TrackUser("gen8ou", "alice");

        // Act
        var result = _manager.GetAllTrackedUsers();

        // Assert
        Assert.That(result, Is.EquivalentTo(new[]
        {
            new EloTrackedUser("gen9ou", "alice"),
            new EloTrackedUser("gen9ou", "bob"),
            new EloTrackedUser("gen8ou", "alice"),
        }));
    }
}
