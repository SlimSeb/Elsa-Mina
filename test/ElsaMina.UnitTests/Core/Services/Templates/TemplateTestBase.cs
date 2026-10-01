using System.Globalization;
using ElsaMina.Commands.Users.Colors;
using ElsaMina.Core.Services.Resources;
using ElsaMina.Core.Services.Templates;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Templates;

/// <summary>
/// Renders the real compiled Razor templates. Localized strings resolve to their key unless a test overrides them
/// with <see cref="SetResourceString"/>, and every user color resolves to <see cref="USER_COLOR"/>.
/// </summary>
[SetCulture("en-US")]
public abstract class TemplateTestBase
{
    protected const string USER_COLOR = "#123456";
    protected static readonly CultureInfo Culture = new("en-US");

    private readonly Dictionary<string, string> _resourceStrings = new();
    private TemplatesManager _templatesManager;

    [OneTimeSetUp]
    public void OneTimeSetUpTemplates()
    {
        var resourcesService = Substitute.For<IResourcesService>();
        resourcesService.GetString(Arg.Any<string>(), Arg.Any<CultureInfo>())
            .Returns(callInfo =>
            {
                var key = callInfo.ArgAt<string>(0);
                return _resourceStrings.GetValueOrDefault(key, key);
            });
        var userColorsService = Substitute.For<IUserColorsService>();
        userColorsService.GetUserColor(Arg.Any<string>()).Returns(USER_COLOR);
        var serviceProvider = new ServiceCollection()
            .AddSingleton(resourcesService)
            .AddSingleton(userColorsService)
            .BuildServiceProvider();

        _templatesManager = new TemplatesManager(serviceProvider);
        _templatesManager.LoadTemplates();
    }

    [SetUp]
    public void SetUpResourceStrings()
    {
        _resourceStrings.Clear();
    }

    protected void SetResourceString(string key, string value)
    {
        _resourceStrings[key] = value;
    }

    protected Task<string> RenderAsync(string templateKey, object model)
    {
        return _templatesManager.GetTemplateAsync(templateKey, model);
    }
}
