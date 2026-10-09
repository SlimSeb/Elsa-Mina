using ElsaMina.Core.Services.Commands;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Services.Commands;

public class CommandRegistryTest
{
    private static ICommand CreateCommand(string name, params string[] aliases)
    {
        var command = Substitute.For<ICommand>();
        command.Name.Returns(name);
        command.Aliases.Returns(aliases);
        return command;
    }

    [Test]
    public void Test_Find_ShouldReturnCommand_WhenSearchingByNameOrAlias()
    {
        // Arrange
        var command = CreateCommand("help", "h", "aide");
        var registry = new CommandRegistry(new Lazy<IEnumerable<ICommand>>(() => [command]));

        // Act & Assert
        Assert.That(registry.Find("help"), Is.SameAs(command));
        Assert.That(registry.Find("h"), Is.SameAs(command));
        Assert.That(registry.Find("aide"), Is.SameAs(command));
    }

    [Test]
    [TestCase("unknown")]
    [TestCase("")]
    [TestCase(null)]
    public void Test_Find_ShouldReturnNull_WhenNameIsUnknown(string name)
    {
        // Arrange
        var registry = new CommandRegistry(new Lazy<IEnumerable<ICommand>>(() => [CreateCommand("help")]));

        // Act & Assert
        Assert.That(registry.Find(name), Is.Null);
    }

    [Test]
    public void Test_Find_ShouldReturnLastRegisteredCommand_WhenNamesCollide()
    {
        // Arrange
        var first = CreateCommand("help");
        var second = CreateCommand("help");
        var registry = new CommandRegistry(new Lazy<IEnumerable<ICommand>>(() => [first, second]));

        // Act & Assert
        Assert.That(registry.Find("help"), Is.SameAs(second));
        Assert.That(registry.Commands, Is.EqualTo(new[] { second }));
    }

    [Test]
    public void Test_Commands_ShouldListEachCommandOnce_WhenCommandHasAliases()
    {
        // Arrange
        var help = CreateCommand("help", "h");
        var ping = CreateCommand("ping");
        var registry = new CommandRegistry(new Lazy<IEnumerable<ICommand>>(() => [help, ping]));

        // Act & Assert
        Assert.That(registry.Commands, Is.EquivalentTo(new[] { help, ping }));
    }

    [Test]
    public void Test_Constructor_ShouldNotResolveCommands_UntilFirstUse()
    {
        // Arrange
        var resolved = false;

        // Act
        var registry = new CommandRegistry(new Lazy<IEnumerable<ICommand>>(() =>
        {
            resolved = true;
            return [];
        }));

        // Assert
        Assert.That(resolved, Is.False);
        _ = registry.Commands;
        Assert.That(resolved, Is.True);
    }
}
