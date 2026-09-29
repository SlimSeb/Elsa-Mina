using ElsaMina.Commands.Economy;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.Economy;

[TestFixture]
public class MoneyLeaderboardTemplateTest : TemplateTestBase
{
    [Test]
    public async Task Test_Render_ShouldNumberRowsInLeaderboardOrder()
    {
        // Arrange
        var model = new MoneyLeaderboardViewModel
        {
            Culture = Culture,
            Leaderboard =
            [
                new KeyValuePair<string, long>("alice", 500),
                new KeyValuePair<string, long>("bob", 300)
            ]
        };

        // Act
        var html = await RenderAsync("Economy/MoneyLeaderboard", model);

        // Assert
        Assert.That(html, Does.Contain("<td>1</td>"));
        Assert.That(html, Does.Contain("<td>2</td>"));
        Assert.That(html, Does.Contain("<td>500 bucks</td>"));
        Assert.That(html, Does.Contain("<td>300 bucks</td>"));
        Assert.That(html.IndexOf("500 bucks", StringComparison.Ordinal),
            Is.LessThan(html.IndexOf("300 bucks", StringComparison.Ordinal)));
    }

    [Test]
    public async Task Test_Render_ShouldColorUserNames()
    {
        // Arrange
        var model = new MoneyLeaderboardViewModel
        {
            Culture = Culture,
            Leaderboard = [new KeyValuePair<string, long>("alice", 500)]
        };

        // Act
        var html = await RenderAsync("Economy/MoneyLeaderboard", model);

        // Assert
        Assert.That(html, Does.Contain("data-name=\"alice\""));
        Assert.That(html, Does.Contain($"color: {USER_COLOR};"));
    }
}
