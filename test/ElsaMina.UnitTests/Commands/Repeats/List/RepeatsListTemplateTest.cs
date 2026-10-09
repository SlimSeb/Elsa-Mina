using ElsaMina.Commands.Repeats;
using ElsaMina.Commands.Repeats.List;
using ElsaMina.UnitTests.Core.Services.Templates;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Repeats.List;

[TestFixture]
public class RepeatsListTemplateTest : TemplateTestBase
{
    [Test]
    public async Task Test_Render_ShouldShowMessageIntervalAndStopButton_ForEachRepeat()
    {
        // Arrange
        var repeatId = Guid.NewGuid();
        var repeat = Substitute.For<IRepeat>();
        repeat.RepeatId.Returns(repeatId);
        repeat.Message.Returns("Remember to hydrate");
        repeat.Interval.Returns(TimeSpan.FromMinutes(30));
        var model = new RepeatsListViewModel
        {
            Culture = Culture,
            Repeats = [repeat],
            BotName = "Bot",
            Trigger = "-"
        };

        // Act
        var html = await RenderAsync("Repeats/List/RepeatsList", model);

        // Assert
        Assert.That(html, Does.Contain("Remember to hydrate"));
        Assert.That(html, Does.Contain("30"));
        Assert.That(html, Does.Contain($"value=\"/w Bot,-stoprepeat {repeatId}\""));
    }
}
