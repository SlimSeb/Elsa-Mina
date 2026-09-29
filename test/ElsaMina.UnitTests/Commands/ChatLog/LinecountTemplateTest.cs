using ElsaMina.Commands.ChatLog;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.ChatLog;

[TestFixture]
public class LinecountTemplateTest : TemplateTestBase
{
    [Test]
    public async Task Test_Render_ShouldShowOneProgressBarPerDay_WithPaddedMonth()
    {
        // Arrange
        var model = new LinecountViewModel
        {
            Culture = Culture,
            Days = [new LinecountDay { Day = 3, Count = 10 }, new LinecountDay { Day = 4, Count = 25 }],
            Month = 7,
            MaxCount = 25,
            TotalCount = 35,
            AvgPerDay = 17.5
        };

        // Act
        var html = await RenderAsync("ChatLog/Linecount", model);

        // Assert
        Assert.That(html, Does.Contain("3/07: <progress max=\"25\" value=\"10\"></progress> 10"));
        Assert.That(html, Does.Contain("4/07: <progress max=\"25\" value=\"25\"></progress> 25"));
    }

    [Test]
    public async Task Test_Render_ShouldShowTotalAndAverage_WithTwoDecimals()
    {
        // Arrange
        SetResourceString("linecount_total", "Total: {0}, average: {1}");
        var model = new LinecountViewModel
        {
            Culture = Culture,
            Days = [],
            Month = 1,
            MaxCount = 0,
            TotalCount = 10,
            AvgPerDay = 10.0 / 3
        };

        // Act
        var html = await RenderAsync("ChatLog/Linecount", model);

        // Assert
        Assert.That(html, Does.Contain("Total: 10, average: 3.33"));
    }
}
