using ElsaMina.Commands.Permissions;
using ElsaMina.Core.Services.Authorization;
using ElsaMina.Core.Services.Commands;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace ElsaMina.UnitTests.Commands.Permissions;

public class PermissionTargetResolverTest
{
    private ICommandRegistry _commandRegistry;
    private PermissionTargetResolver _resolver;

    [SetUp]
    public void SetUp()
    {
        var addBadge = CreateCommand("add-badge", "Badges");
        var badgeList = CreateCommand("badgelist", "Badges");
        var dollList = CreateCommand("dolllist", "Dolls");
        _commandRegistry = Substitute.For<ICommandRegistry>();
        _commandRegistry.Find(Arg.Any<string>()).ReturnsNull();
        _commandRegistry.Find("add-badge").Returns(addBadge);
        _commandRegistry.Find("addbadge").Returns(addBadge);
        _commandRegistry.Find("badgelist").Returns(badgeList);
        _commandRegistry.Find("dolls").Returns(dollList);
        _commandRegistry.Commands.Returns([addBadge, badgeList, dollList]);
        _resolver = new PermissionTargetResolver(_commandRegistry);
    }

    [Test]
    [TestCase("+", "+")]
    [TestCase("%", "%")]
    [TestCase("@", "@")]
    [TestCase("#", "#")]
    [TestCase("voice", "+")]
    [TestCase("Driver", "%")]
    [TestCase("moderator", "@")]
    public void Test_Resolve_ShouldReturnRank_WhenTextIsARankSymbolOrName(string text, string expectedSymbol)
    {
        // Act
        var target = _resolver.Resolve(text);

        // Assert
        Assert.That(target, Is.EqualTo(new PermissionTarget(PermissionGrantKind.Rank, expectedSymbol)));
    }

    [Test]
    [TestCase("add-badge")]
    [TestCase("AddBadge")]
    [TestCase(" addbadge ")]
    public void Test_Resolve_ShouldReturnCommandMainName_WhenTextIsACommandNameOrAlias(string text)
    {
        // Act
        var target = _resolver.Resolve(text);

        // Assert
        Assert.That(target, Is.EqualTo(new PermissionTarget(PermissionGrantKind.Command, "add-badge")));
    }

    [Test]
    [TestCase("badges", "Badges")]
    [TestCase("category:badges", "Badges")]
    [TestCase("Category: Dolls", "Dolls")]
    public void Test_Resolve_ShouldReturnCategory_WhenTextIsACategory(string text, string expectedCategory)
    {
        // Act
        var target = _resolver.Resolve(text);

        // Assert
        Assert.That(target, Is.EqualTo(new PermissionTarget(PermissionGrantKind.Category, expectedCategory)));
    }

    [Test]
    public void Test_Resolve_ShouldPreferCommand_WhenACommandHasTheNameOfACategory()
    {
        // Act
        var target = _resolver.Resolve("dolls");

        // Assert
        Assert.That(target, Is.EqualTo(new PermissionTarget(PermissionGrantKind.Command, "dolllist")));
    }

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    [TestCase("unknown")]
    [TestCase("category:unknown")]
    public void Test_Resolve_ShouldReturnNull_WhenTextIsNothingKnown(string text)
    {
        // Act & Assert
        Assert.That(_resolver.Resolve(text), Is.Null);
    }

    private static ICommand CreateCommand(string name, string category)
    {
        var command = Substitute.For<ICommand>();
        command.Name.Returns(name);
        command.Category.Returns(category);
        return command;
    }
}
