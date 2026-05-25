using ElsaMina.Commands.Development;
using ElsaMina.Core.Contexts;
using ElsaMina.Core.Services.FeatureSwitches;
using NSubstitute;

namespace ElsaMina.UnitTests.Commands.Development;

public class FeatureSwitchCommandTest
{
    private IFeatureSwitchService _featureSwitchService;
    private IContext _context;
    private FeatureSwitchCommand _command;

    [SetUp]
    public void SetUp()
    {
        _featureSwitchService = Substitute.For<IFeatureSwitchService>();
        _context = Substitute.For<IContext>();
        _command = new FeatureSwitchCommand(_featureSwitchService);
    }

    [Test]
    public void Test_Constructor_ShouldInitializeCommand_WhenCalled()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_command.Name, Is.EqualTo("featureswitch"));
            Assert.That(_command.Aliases, Contains.Item("fs"));
        }
    }

    [Test]
    public async Task Test_RunAsync_ShouldDisableFeature_WhenFeatureIsCurrentlyEnabled()
    {
        _context.Target.Returns("SomeFeature");
        _featureSwitchService.IsFeatureEnabled("SomeFeature").Returns(true);

        await _command.RunAsync(_context);

        _featureSwitchService.Received(1).SetFeature("SomeFeature", false);
        _context.Received(1).ReplyLocalizedMessage("featureswitch_disabled", "SomeFeature");
    }

    [Test]
    public async Task Test_RunAsync_ShouldEnableFeature_WhenFeatureIsCurrentlyDisabled()
    {
        _context.Target.Returns("SomeFeature");
        _featureSwitchService.IsFeatureEnabled("SomeFeature").Returns(false);

        await _command.RunAsync(_context);

        _featureSwitchService.Received(1).SetFeature("SomeFeature", true);
        _context.Received(1).ReplyLocalizedMessage("featureswitch_enabled", "SomeFeature");
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyNoneDisabled_WhenNoArgumentAndNoFeaturesDisabled()
    {
        _context.Target.Returns(string.Empty);
        _featureSwitchService.DisabledFeatures.Returns([]);

        await _command.RunAsync(_context);

        _featureSwitchService.DidNotReceive().SetFeature(Arg.Any<string>(), Arg.Any<bool>());
        _context.Received(1).ReplyLocalizedMessage("featureswitch_none_disabled", string.Empty);
    }

    [Test]
    public async Task Test_RunAsync_ShouldReplyDisabledList_WhenNoArgumentAndSomeFeaturesDisabled()
    {
        _context.Target.Returns(string.Empty);
        _featureSwitchService.DisabledFeatures.Returns(["FeatureA", "FeatureB"]);

        await _command.RunAsync(_context);

        _featureSwitchService.DidNotReceive().SetFeature(Arg.Any<string>(), Arg.Any<bool>());
        _context.Received(1).ReplyLocalizedMessage("featureswitch_disabled_list", "FeatureA, FeatureB");
    }

    [Test]
    public async Task Test_RunAsync_ShouldTrimTarget_WhenTargetHasLeadingOrTrailingWhitespace()
    {
        _context.Target.Returns("  SomeFeature  ");
        _featureSwitchService.IsFeatureEnabled("SomeFeature").Returns(true);

        await _command.RunAsync(_context);

        _featureSwitchService.Received(1).SetFeature("SomeFeature", false);
    }
}
