using ElsaMina.Commands.Dolls;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.Dolls;

[TestFixture]
public class DollTemplateTest : TemplateTestBase
{
    [Test]
    public async Task Test_Render_ShouldRenderTrimmedImageWithSizeAndName()
    {
        // Arrange
        var doll = new Doll { Id = "pikachu", Name = "Pikachu", Size = 64, Image = "  https://example.com/pikachu.png " };

        // Act
        var html = await RenderAsync("Dolls/Doll", doll);

        // Assert
        Assert.That(html, Does.Contain("src=\"https://example.com/pikachu.png\""));
        Assert.That(html, Does.Contain("width=\"64\""));
        Assert.That(html, Does.Contain("height=\"64\""));
        Assert.That(html, Does.Contain("title=\"Pikachu\""));
        Assert.That(html, Does.Contain("alt=\"Pikachu\""));
    }
}
