using System.Reflection;
using System.Xml.Linq;
using ElsaMina.Battles;
using ElsaMina.Cloud;
using ElsaMina.Commands;
using ElsaMina.Core;
using ElsaMina.DataAccess;
using ElsaMina.Logging;

namespace ElsaMina.UnitTests.Architecture;

/// <summary>
/// Vérifie les frontières entre projets décrites dans CLAUDE.md : Core c'est le noyau du runtime et connaît aucune
/// feature ni infra ; les projets de features dépendent jamais les uns des autres ; rien dépend de la console
/// </summary>
public class ProjectDependenciesTest
{
    private static readonly string[] INFRASTRUCTURE_PACKAGES =
        ["Microsoft.EntityFrameworkCore", "Npgsql", "Google.", "AWSSDK."];

    private static IEnumerable<TestCaseData> ForbiddenDependencies()
    {
        yield return new TestCaseData(typeof(Bot).Assembly,
                new[] { "ElsaMina.Commands", "ElsaMina.Battles", "ElsaMina.DataAccess", "ElsaMina.Cloud",
                    "ElsaMina.Console" })
            .SetName("Core depends on no feature, infrastructure or host project");
        yield return new TestCaseData(typeof(BattlesModule).Assembly, new[] { "ElsaMina.Commands", "ElsaMina.Console" })
            .SetName("Battles does not depend on Commands");
        yield return new TestCaseData(typeof(CommandModule).Assembly, new[] { "ElsaMina.Battles", "ElsaMina.Console" })
            .SetName("Commands does not depend on Battles");
        yield return new TestCaseData(typeof(BotDbContext).Assembly,
                new[] { "ElsaMina.Commands", "ElsaMina.Battles", "ElsaMina.Cloud", "ElsaMina.Console" })
            .SetName("DataAccess depends on no feature project");
        yield return new TestCaseData(typeof(CloudModule).Assembly,
                new[] { "ElsaMina.Core", "ElsaMina.Commands", "ElsaMina.Battles", "ElsaMina.DataAccess",
                    "ElsaMina.Console" })
            .SetName("Cloud depends on no other project");
        yield return new TestCaseData(typeof(Log).Assembly,
                new[] { "ElsaMina.Core", "ElsaMina.Commands", "ElsaMina.Battles", "ElsaMina.DataAccess",
                    "ElsaMina.Cloud", "ElsaMina.Console" })
            .SetName("Logging depends on no other project");
    }

    [TestCaseSource(nameof(ForbiddenDependencies))]
    public void Test_Assembly_ShouldNotReferenceForbiddenProjects(Assembly assembly, string[] forbiddenProjects)
    {
        // Act
        var compiledReferences = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToList();
        var declaredReferences = GetDeclaredProjectReferences(assembly.GetName().Name);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(compiledReferences.Intersect(forbiddenProjects), Is.Empty, "Compiled references");
            Assert.That(declaredReferences.Intersect(forbiddenProjects), Is.Empty, "csproj ProjectReference items");
        }
    }

    [Test]
    public void Test_Core_ShouldNotReferenceInfrastructurePackages()
    {
        // Act
        var references = typeof(Bot).Assembly.GetReferencedAssemblies().Select(reference => reference.Name);

        // Assert
        Assert.That(references.Where(name => INFRASTRUCTURE_PACKAGES.Any(name.StartsWith)), Is.Empty);
    }

    private static IEnumerable<string> GetDeclaredProjectReferences(string projectName)
    {
        var projectFile = Path.Combine(FindRepositoryRoot(), "src", projectName, $"{projectName}.csproj");
        return XDocument.Load(projectFile)
            .Descendants("ProjectReference")
            .Select(reference => Path.GetFileNameWithoutExtension(
                reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar)))
            .ToList();
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
}
