using ElsaMina.Commands.Games.GuessingGame;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.Games.GuessingGame;

[TestFixture]
public class GuessingGameResultTemplateTest : TemplateTestBase
{
    [Test]
    public async Task Test_Render_ShouldRankPlayersByDescendingScore()
    {
        // Arrange
        var model = new GuessingGameResultViewModel
        {
            Culture = Culture,
            Scores = new Dictionary<GuessingGamePlayer, int>
            {
                [new GuessingGamePlayer("bob", "Bob")] = 2,
                [new GuessingGamePlayer("alice", "Alice")] = 5
            }
        };

        // Act
        var html = await RenderAsync("Games/GuessingGame/GuessingGameResult", model);

        // Assert
        Assert.That(html, Does.Contain($"1. <span class=\"username\" data-name=\"alice\" style=\"color: {USER_COLOR}\">Alice</span>"));
        Assert.That(html, Does.Contain($"2. <span class=\"username\" data-name=\"bob\" style=\"color: {USER_COLOR}\">Bob</span>"));
    }

    [Test]
    public async Task Test_Render_ShouldPluralizePoints_OnlyWhenScoreIsNotOne()
    {
        // Arrange
        SetResourceString("guessing_game_point", "point");
        var model = new GuessingGameResultViewModel
        {
            Culture = Culture,
            Scores = new Dictionary<GuessingGamePlayer, int>
            {
                [new GuessingGamePlayer("alice", "Alice")] = 3,
                [new GuessingGamePlayer("bob", "Bob")] = 1
            }
        };

        // Act
        var html = await RenderAsync("Games/GuessingGame/GuessingGameResult", model);

        // Assert
        Assert.That(html, Does.Contain("(3 points)"));
        Assert.That(html, Does.Contain("(1 point)"));
    }

    [Test]
    public async Task Test_Render_ShouldNotShowScores_WhenNobodyScored()
    {
        // Arrange
        var model = new GuessingGameResultViewModel
        {
            Culture = Culture,
            Scores = new Dictionary<GuessingGamePlayer, int>()
        };

        // Act
        var html = await RenderAsync("Games/GuessingGame/GuessingGameResult", model);

        // Assert
        Assert.That(html, Does.Contain("guessing_game_result_ended"));
        Assert.That(html, Does.Not.Contain("guessing_game_final_scores"));
    }
}
