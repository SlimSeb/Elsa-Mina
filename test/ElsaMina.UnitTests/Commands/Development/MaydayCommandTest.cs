using ElsaMina.Commands.Development;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.FeatureSwitches;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Development;

public class MaydayCommandTest
{
    private IFeatureSwitchService _featureSwitchService;
    private IContext _context;
    private MaydayCommand _command;

    [SetUp]
    public void SetUp()
    {
        _featureSwitchService = Substitute.For<IFeatureSwitchService>();
        _context = Substitute.For<IContext>();
        _command = new MaydayCommand(_featureSwitchService);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        Assert.That(_command.Name, Is.EqualTo("mayday"));
    }

    [Test]
    public async Task Test_RunAsync_ShouldActivateMayday_WhenCurrentlyInactive()
    {
        _featureSwitchService.IsMaydayActive.Returns(false);

        await _command.RunAsync(_context);

        _featureSwitchService.Received(1).SetMayday(true);
        _context.Received(1).ReplyLocalizedMessage("mayday_activated");
    }

    [Test]
    public async Task Test_RunAsync_ShouldDeactivateMayday_WhenCurrentlyActive()
    {
        _featureSwitchService.IsMaydayActive.Returns(true);

        await _command.RunAsync(_context);

        _featureSwitchService.Received(1).SetMayday(false);
        _context.Received(1).ReplyLocalizedMessage("mayday_deactivated");
    }
}
