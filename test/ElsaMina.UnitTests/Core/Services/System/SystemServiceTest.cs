using ElsaMina.Core.Services.System;

namespace ElsaMina.UnitTests.Core.Services.System;

[TestFixture]
public class SystemServiceTest
{
    private SystemService _systemService;

    [SetUp]
    public void SetUp()
    {
        _systemService = new SystemService();
    }

    [Test]
    public void Test_GetSystemInfo_ShouldReturnPopulatedSystemInfo()
    {
        var info = _systemService.GetSystemInfo();

        Assert.That(info, Is.Not.Null);
        Assert.That(info.FrameworkDescription, Is.Not.Null.And.Not.Empty);
        Assert.That(info.RuntimeIdentifier, Is.Not.Null.And.Not.Empty);
        Assert.That(info.OsDescription, Is.Not.Null.And.Not.Empty);
        Assert.That(info.ProcessorCount, Is.GreaterThan(0));
        Assert.That(info.ProcessId, Is.GreaterThan(0));
        Assert.That(info.WorkingSet, Is.GreaterThan(0));
        Assert.That(info.PeakWorkingSet, Is.GreaterThanOrEqualTo(info.WorkingSet));
        Assert.That(info.GcTotalMemory, Is.GreaterThan(0));
    }
}
