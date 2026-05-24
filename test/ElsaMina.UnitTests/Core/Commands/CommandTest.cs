using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.Commands;
using NSubstitute;

namespace ElsaMina.UnitTests.Core.Commands;

public class CommandTest
{
    private Action _action;
    private TestCommand _testCommand;

    [SetUp]
    public void SetUp()
    {
        _action = Substitute.For<Action>();
        _testCommand = new TestCommand(_action);
    }

    [Test]
    public void Test_Constructor_ShouldInitializePropertiesFromAttribute()
    {
        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_testCommand.Name, Is.EqualTo("test-command"));
            Assert.That(_testCommand.Aliases, Is.EquivalentTo(new List<string> { "alias1", "alias2" }));
        }
    }

    [NamedCommand("test-command", "alias1", "alias2")]
    private class TestCommand : Command
    {
        private readonly Action _action;

        public TestCommand(Action action)
        {
            _action = action;
        }

        public string CommandHelpMessageKey { get; set; }

        public override string HelpMessageKey => CommandHelpMessageKey;

        public override Task RunAsync(IContext context, CancellationToken cancellationToken = default)
        {
            _action.Invoke();
            return Task.CompletedTask;
        }
    }
}