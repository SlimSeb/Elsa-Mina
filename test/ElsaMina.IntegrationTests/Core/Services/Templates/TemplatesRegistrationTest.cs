using ElsaMina.Commands;
using ElsaMina.Core.Services.Templates;
using Microsoft.AspNetCore.Components;

namespace ElsaMina.IntegrationTests.Core.Services.Templates;

/// <summary>
/// Checks the convention TemplatesManager relies on: each .razor file compiles to a component whose namespace mirrors
/// its folder, so the template key is the folder path plus the file name, and which accepts a Model parameter.
/// </summary>
public class TemplatesRegistrationTest
{
    private const string TEMPLATES_NAMESPACE = "ElsaMina.Templates";
    private const string COMMANDS_PROJECT_FOLDER = "ElsaMina.Commands";

    private static IEnumerable<string> GetTemplateKeys()
    {
        var commandsFolder = Path.Combine(FindRepositoryRoot(), "src", COMMANDS_PROJECT_FOLDER);
        return Directory.EnumerateFiles(commandsFolder, "*.razor", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && Path.GetFileName(path) != "_Imports.razor")
            .Select(path => Path.ChangeExtension(Path.GetRelativePath(commandsFolder, path), null)
                .Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(key => key);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElsaMina.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find the repository root");
    }

    private static Type FindTemplateType(string templateKey)
    {
        var typeName = $"{TEMPLATES_NAMESPACE}.{templateKey.Replace('/', '.')}";
        return typeof(CommandModule).Assembly.GetType(typeName);
    }

    [Test]
    public void Test_TemplateKeys_ShouldNotBeEmpty()
    {
        Assert.That(GetTemplateKeys(), Is.Not.Empty);
    }

    [TestCaseSource(nameof(GetTemplateKeys))]
    public void Test_Template_ShouldCompileToComponentMatchingItsKey(string templateKey)
    {
        var templateType = FindTemplateType(templateKey);

        Assert.That(templateType, Is.Not.Null, $"No component found for template '{templateKey}'");
        Assert.That(typeof(IComponent).IsAssignableFrom(templateType), Is.True);
    }

    [TestCaseSource(nameof(GetTemplateKeys))]
    public void Test_Template_ShouldInheritTemplatePage(string templateKey)
    {
        var templateType = FindTemplateType(templateKey);

        var inheritsTemplatePage = false;
        for (var type = templateType; type != null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(TemplatePage<>))
            {
                inheritsTemplatePage = true;
                break;
            }
        }

        Assert.That(inheritsTemplatePage, Is.True,
            $"Template '{templateKey}' must inherit TemplatePage<TModel> to receive its Model parameter");
    }
}
