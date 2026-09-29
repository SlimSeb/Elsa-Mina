using ElsaMina.Commands.Arcade.Points;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.Arcade.Points;

[TestFixture]
public class PointsUpdateTemplateTest : TemplateTestBase
{
    private static PointsUpdateViewModel CreateModel(double pointsAdded, double newTotal, bool isAddition) => new()
    {
        Culture = Culture,
        Username = "alice",
        PointsAdded = pointsAdded,
        NewTotal = newTotal,
        IsAddition = isAddition,
        Leaderboard = new Dictionary<string, double> { ["alice"] = newTotal, ["bob"] = 1.25 }
    };

    [Test]
    public async Task Test_Render_ShouldUseAddedMessage_WhenPointsAreAdded()
    {
        // Act
        var html = await RenderAsync("Arcade/Points/PointsUpdate", CreateModel(3, 10, isAddition: true));

        // Assert
        Assert.That(html, Does.Contain("<b>3 points_added_to alice</b>"));
        Assert.That(html, Does.Not.Contain("points_removed_from"));
    }

    [Test]
    public async Task Test_Render_ShouldUseRemovedMessage_WhenPointsAreRemoved()
    {
        // Act
        var html = await RenderAsync("Arcade/Points/PointsUpdate", CreateModel(3, 7, isAddition: false));

        // Assert
        Assert.That(html, Does.Contain("<b>3 points_removed_from alice</b>"));
    }

    [Test]
    public async Task Test_Render_ShouldFormatPoints_WithoutDecimalsForWholeNumbersAndAtMostTwoOtherwise()
    {
        // Act
        var html = await RenderAsync("Arcade/Points/PointsUpdate", CreateModel(0.5, 10, isAddition: true));

        // Assert
        Assert.That(html, Does.Contain("<b>0.5 points_added_to alice</b>"));
        Assert.That(html, Does.Contain("<b>alice</b> : 10<br>"));
        Assert.That(html, Does.Contain("<td>1.25</td>"));
        Assert.That(html, Does.Contain("<td>10</td>"));
    }
}
