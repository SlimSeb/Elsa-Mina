using ElsaMina.Core.Services.FeatureSwitches;

namespace ElsaMina.UnitTests.Core.Services.FeatureSwitches;

public class FeatureSwitchServiceTest
{
    private FeatureSwitchService _featureSwitchService;

    [SetUp]
    public void SetUp()
    {
        _featureSwitchService = new FeatureSwitchService();
    }

    [Test]
    public void Test_IsMaydayActive_ShouldReturnFalse_ByDefault()
    {
        Assert.That(_featureSwitchService.IsMaydayActive, Is.False);
    }

    [Test]
    public void Test_SetMayday_ShouldActivateMayday_WhenSetToTrue()
    {
        _featureSwitchService.SetMayday(true);

        Assert.That(_featureSwitchService.IsMaydayActive, Is.True);
    }

    [Test]
    public void Test_SetMayday_ShouldDeactivateMayday_WhenSetToFalse()
    {
        _featureSwitchService.SetMayday(true);
        _featureSwitchService.SetMayday(false);

        Assert.That(_featureSwitchService.IsMaydayActive, Is.False);
    }

    [Test]
    public void Test_IsFeatureEnabled_ShouldReturnTrue_WhenFeatureNotExplicitlySet()
    {
        Assert.That(_featureSwitchService.IsFeatureEnabled("SomeFeature"), Is.True);
    }

    [Test]
    public void Test_SetFeature_ShouldDisableFeature_WhenSetToFalse()
    {
        _featureSwitchService.SetFeature("SomeFeature", false);

        Assert.That(_featureSwitchService.IsFeatureEnabled("SomeFeature"), Is.False);
    }

    [Test]
    public void Test_SetFeature_ShouldEnableFeature_WhenSetToTrue()
    {
        _featureSwitchService.SetFeature("SomeFeature", false);
        _featureSwitchService.SetFeature("SomeFeature", true);

        Assert.That(_featureSwitchService.IsFeatureEnabled("SomeFeature"), Is.True);
    }

    [Test]
    public void Test_DisabledFeatures_ShouldBeEmpty_ByDefault()
    {
        Assert.That(_featureSwitchService.DisabledFeatures, Is.Empty);
    }

    [Test]
    public void Test_DisabledFeatures_ShouldContainDisabledFeature_WhenFeatureIsDisabled()
    {
        _featureSwitchService.SetFeature("SomeFeature", false);

        Assert.That(_featureSwitchService.DisabledFeatures, Contains.Item("SomeFeature"));
    }

    [Test]
    public void Test_DisabledFeatures_ShouldNotContainEnabledFeature_WhenFeatureIsEnabled()
    {
        _featureSwitchService.SetFeature("SomeFeature", false);
        _featureSwitchService.SetFeature("SomeFeature", true);

        Assert.That(_featureSwitchService.DisabledFeatures, Does.Not.Contain("SomeFeature"));
    }

    [Test]
    public void Test_DisabledFeatures_ShouldOnlyContainDisabledFeatures_WhenMultipleFeaturesAreSet()
    {
        _featureSwitchService.SetFeature("FeatureA", false);
        _featureSwitchService.SetFeature("FeatureB", true);
        _featureSwitchService.SetFeature("FeatureC", false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_featureSwitchService.DisabledFeatures, Contains.Item("FeatureA"));
            Assert.That(_featureSwitchService.DisabledFeatures, Does.Not.Contain("FeatureB"));
            Assert.That(_featureSwitchService.DisabledFeatures, Contains.Item("FeatureC"));
        }
    }
}
