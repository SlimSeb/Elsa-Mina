using ElsaMina.Commands.Repeats.Form;
using ElsaMina.UnitTests.Core.Services.Templates;

namespace ElsaMina.UnitTests.Commands.Repeats.Form;

[TestFixture]
public class RepeatFormTemplateTest : TemplateTestBase
{
    [Test]
    public async Task Test_Render_ShouldSubmitFormWithModelCommand()
    {
        // Arrange
        var model = new RepeatFormViewModel
        {
            Culture = Culture,
            Command = "/w Bot,-repeat lobby, {delay}, {message}"
        };

        // Act
        var html = await RenderAsync("Repeats/Form/RepeatForm", model);

        // Assert
        Assert.That(html, Does.Contain("<form data-submitsend=\"/w Bot,-repeat lobby, {delay}, {message}\">"));
        Assert.That(html, Does.Contain("name=\"delay\""));
        Assert.That(html, Does.Contain("name=\"message\""));
    }
}
