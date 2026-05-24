using ElsaMina.Commands.Showdown.Ladder;

namespace ElsaMina.UnitTests.Commands.Showdown.Ladder;

public class LadderHistoryManagerTest
{
    [Test]
    public void Test_GetPreviousEntriesAndSave_ShouldReturnEmpty_OnFirstSnapshot()
    {
        // Arrange
        var manager = new LadderHistoryManager();
        var players = new[]
        {
            new LadderPlayerDto { UserId = "alice", Username = "Alice", Elo = 1500 }
        };

        // Act
        var previousEntries = manager.GetPreviousEntriesAndSave("gen9ou", players);

        // Assert
        Assert.That(previousEntries, Is.Empty);
    }

    [Test]
    public void Test_GetPreviousEntriesAndSave_ShouldReturnPreviousSnapshot_OnSecondCall()
    {
        // Arrange
        var manager = new LadderHistoryManager();
        manager.GetPreviousEntriesAndSave("gen9ou",
        [
            new LadderPlayerDto { UserId = "alice", Username = "Alice", Elo = 1500 }
        ]);

        // Act
        var previousEntries = manager.GetPreviousEntriesAndSave("gen9ou",
        [
            new LadderPlayerDto { UserId = "alice", Username = "Alice", Elo = 1512 }
        ]);

        // Assert
        Assert.That(previousEntries["alice"], Is.EqualTo(1500));
    }

    [Test]
    public void Test_GetPreviousEntriesAndSave_ShouldUseUsername_WhenUserIdIsMissing()
    {
        // Arrange
        var manager = new LadderHistoryManager();
        manager.GetPreviousEntriesAndSave("gen9ou",
        [
            new LadderPlayerDto { UserId = "", Username = "Alice", Elo = 1500 }
        ]);

        // Act
        var previousEntries = manager.GetPreviousEntriesAndSave("gen9ou",
        [
            new LadderPlayerDto { UserId = "", Username = "Alice", Elo = 1490 }
        ]);

        // Assert
        Assert.That(previousEntries["alice"], Is.EqualTo(1500));
    }

    [Test]
    public void Test_GetPreviousPlacementsAndSave_ShouldReturnPreviousPlacement_OnSecondCall()
    {
        // Arrange
        var manager = new LadderHistoryManager();
        manager.GetPreviousPlacementsAndSave("gen9ou",
        [
            new LadderPlayerDto { UserId = "alice", Username = "Alice", Index = 4 }
        ]);

        // Act
        var previousEntries = manager.GetPreviousPlacementsAndSave("gen9ou",
        [
            new LadderPlayerDto { UserId = "alice", Username = "Alice", Index = 2 }
        ]);

        // Assert
        Assert.That(previousEntries["alice"], Is.EqualTo(4));
    }

    [Test]
    public void Test_GetPreviousPrefixedPlacementsAndSave_ShouldScopePlacementsByPrefix()
    {
        // Arrange
        var manager = new LadderHistoryManager();
        manager.GetPreviousPrefixedPlacementsAndSave("gen9ou", "pla",
        [
            new LadderPlayerDto { UserId = "alice", Username = "Alice", InnerIndex = 2 }
        ]);

        // Act
        var previousForOtherPrefix = manager.GetPreviousPrefixedPlacementsAndSave("gen9ou", "xyz",
        [
            new LadderPlayerDto { UserId = "alice", Username = "Alice", InnerIndex = 1 }
        ]);

        var previousForSamePrefix = manager.GetPreviousPrefixedPlacementsAndSave("gen9ou", "pla",
        [
            new LadderPlayerDto { UserId = "alice", Username = "Alice", InnerIndex = 1 }
        ]);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(previousForOtherPrefix, Is.Empty);
            Assert.That(previousForSamePrefix["alice"], Is.EqualTo(2));
        }
    }
}
