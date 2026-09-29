using ElsaMina.Commands.CustomCommands;
using ElsaMina.DataAccess.Models;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.CustomCommands;

[TestFixture]
public class CustomCommandListTemplateTest : TemplateTestBase
{
    [Test]
    public async Task Test_Render_ShouldShowOneRowPerCommand()
    {
        // Arrange
        var model = new CustomCommandListViewModel
        {
            Culture = Culture,
            Commands =
            [
                new AddedCommand { Id = "hello", Content = "Hello world", Author = "alice" },
                new AddedCommand { Id = "bye", Content = "Goodbye", Author = "bob" }
            ]
        };

        // Act
        var html = await RenderAsync("CustomCommands/CustomCommandList", model);

        // Assert
        Assert.That(html, Does.Contain("<b>hello</b>"));
        Assert.That(html, Does.Contain(">Hello world</td>"));
        Assert.That(html, Does.Contain(">alice</td>"));
        Assert.That(html, Does.Contain("<b>bye</b>"));
    }

    [Test]
    public async Task Test_Render_ShouldEncodeCommandContent()
    {
        // Arrange
        var model = new CustomCommandListViewModel
        {
            Culture = Culture,
            Commands = [new AddedCommand { Id = "html", Content = "<img src=x>", Author = "alice" }]
        };

        // Act
        var html = await RenderAsync("CustomCommands/CustomCommandList", model);

        // Assert
        Assert.That(html, Does.Not.Contain("<img src=x>"));
        Assert.That(html, Does.Contain("&lt;img src=x&gt;"));
    }
}
