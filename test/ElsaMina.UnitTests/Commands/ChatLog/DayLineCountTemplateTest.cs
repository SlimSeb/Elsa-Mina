using ElsaMina.Commands.ChatLog;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.ChatLog;

[TestFixture]
public class DayLineCountTemplateTest : TemplateTestBase
{
    [Test]
    public async Task Test_Render_ShouldShowCountsAndRatios_ForEachRow()
    {
        // Arrange
        var model = new DayLineCountViewModel
        {
            Culture = Culture,
            Rows = [new DayLineCountRow { UserId = "alice", Color = "#ff0000", Messages = 4, Words = 10, Chars = 50 }]
        };

        // Act
        var html = await RenderAsync("ChatLog/DayLineCount", model);

        // Assert
        Assert.That(html, Does.Contain("data-name=\"alice\" style=\"color: #ff0000\">alice</span>"));
        Assert.That(html, Does.Contain(">4</td>"));
        Assert.That(html, Does.Contain("10 <small>(2.5)</small>"));
        Assert.That(html, Does.Contain("50 <small>(12.5)</small>"));
    }

    [Test]
    public async Task Test_Render_ShouldOnlyShowHeaders_WhenThereAreNoRows()
    {
        // Arrange
        var model = new DayLineCountViewModel { Culture = Culture, Rows = [] };

        // Act
        var html = await RenderAsync("ChatLog/DayLineCount", model);

        // Assert
        Assert.That(html, Does.Contain("daylinecount_header_user"));
        Assert.That(html, Does.Not.Contain("class=\"username\""));
    }
}
