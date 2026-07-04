using ElsaMina.Battles;

namespace ElsaMina.UnitTests.Battles;

public class BattleMessageParserHazardTest
{
    private BattleMessageParser _parser;
    private BattleContext _context;

    [SetUp]
    public void SetUp()
    {
        _parser = new BattleMessageParser();
        _context = new BattleContext("battle-gen9ou-123456") { SideId = "p1" };
    }

    private void Apply(string message)
    {
        _parser.TryApplyMessage(message.Split('|'), _context.RoomId, _context, out _);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldTrackStealthRock_WhenSetOnOurSide()
    {
        // Act
        Apply("|-sidestart|p1: Slim|move: Stealth Rock");

        // Assert
        Assert.That(_context.OwnSideStealthRock, Is.True);
    }

    [Test]
    public void Test_TryApplyMessage_ShouldStackSpikesLayers_WhenSetMultipleTimes()
    {
        // Act
        Apply("|-sidestart|p1: Slim|Spikes");
        Apply("|-sidestart|p1: Slim|Spikes");
        Apply("|-sidestart|p1: Slim|Spikes");
        Apply("|-sidestart|p1: Slim|Spikes");

        // Assert - Spikes cap at three layers
        Assert.That(_context.OwnSideSpikesLayers, Is.EqualTo(3));
    }

    [Test]
    public void Test_TryApplyMessage_ShouldIgnoreHazards_WhenSetOnOpponentSide()
    {
        // Act
        Apply("|-sidestart|p2: Rival|move: Stealth Rock");
        Apply("|-sidestart|p2: Rival|Spikes");

        // Assert - opponent-side hazards are not tracked
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.OwnSideStealthRock, Is.False);
            Assert.That(_context.OwnSideSpikesLayers, Is.EqualTo(0));
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldTrackOpponentSideHazards_WhenWeSetThem()
    {
        // Act
        Apply("|-sidestart|p2: Rival|move: Stealth Rock");
        Apply("|-sidestart|p2: Rival|Spikes");
        Apply("|-sidestart|p2: Rival|move: Toxic Spikes");
        Apply("|-sidestart|p2: Rival|move: Sticky Web");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.OpponentSideStealthRock, Is.True);
            Assert.That(_context.OpponentSideSpikesLayers, Is.EqualTo(1));
            Assert.That(_context.OpponentSideToxicSpikes, Is.True);
            Assert.That(_context.OpponentSideStickyWeb, Is.True);
        }
    }

    [Test]
    public void Test_TryApplyMessage_ShouldClearHazards_WhenRemovedFromOurSide()
    {
        // Arrange
        Apply("|-sidestart|p1: Slim|move: Stealth Rock");
        Apply("|-sidestart|p1: Slim|Spikes");
        Apply("|-sidestart|p1: Slim|Spikes");

        // Act
        Apply("|-sideend|p1: Slim|Stealth Rock|[from] move: Rapid Spin");
        Apply("|-sideend|p1: Slim|Spikes|[from] move: Rapid Spin");

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_context.OwnSideStealthRock, Is.False);
            Assert.That(_context.OwnSideSpikesLayers, Is.EqualTo(0));
        }
    }
}
