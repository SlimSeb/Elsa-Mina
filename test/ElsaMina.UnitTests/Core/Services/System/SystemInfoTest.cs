using ElsaMina.Core.Services.System;

namespace ElsaMina.UnitTests.Core.Services.System;

[TestFixture]
public class SystemInfoTest
{
    [Test]
    public void Test_ToString_ShouldFormatCorrectly_WithCompleteData()
    {
        var systemInfo = new SystemInfo
        {
            FrameworkDescription = ".NET 10.0.0",
            RuntimeIdentifier = "osx-arm64",
            OsDescription = "macOS 15.3.1",
            OsArchitecture = "Arm64",
            ProcessArchitecture = "Arm64",
            ProcessorCount = 8,
            ProcessId = 1234,
            ThreadCount = 24,
            Uptime = new TimeSpan(1, 2, 3, 4), // 1d 2h 3m 4s
            CpuTime = TimeSpan.FromSeconds(12.5),
            WorkingSet = 64 * 1024 * 1024,
            PeakWorkingSet = 80 * 1024 * 1024,
            PrivateMemory = 45 * 1024 * 1024,
            PagedMemory = 40 * 1024 * 1024,
            VirtualMemory = 500 * 1024 * 1024,
            GcTotalMemory = 20 * 1024 * 1024,
            GcTotalAllocatedMemory = 100 * 1024 * 1024,
            TotalAvailableMemory = 16L * 1024 * 1024 * 1024,
            Gen0Collections = 10,
            Gen1Collections = 3,
            Gen2Collections = 1
        };

        var result = systemInfo.ToString();

        Assert.That(result, Does.Contain("OS: macOS 15.3.1 (Arm64) | Runtime: .NET 10.0.0 (osx-arm64)"));
        Assert.That(result, Does.Contain("CPU: 8 cores | PID: 1234 | Threads: 24 | Uptime: 1d 2h 3m 4s | CPU Time: 12.5s"));
        Assert.That(result, Does.Contain("Memory: WS: 64 MB (Peak: 80 MB) | Priv: 45 MB | Paged: 40 MB | Virt: 500 MB"));
        Assert.That(result, Does.Contain("GC: Heap: 20 MB | Allocated: 100 MB | Gen 0/1/2: 10/3/1 | Available: 16 GB"));
    }

    [Test]
    public void Test_ToString_ShouldHandleDefaultValuesWithoutThrowing()
    {
        var systemInfo = new SystemInfo();

        var result = systemInfo.ToString();

        Assert.That(result, Does.Contain("OS: Unknown OS | Runtime: Unknown Runtime"));
        Assert.That(result, Does.Contain("CPU: 0 cores | PID: 0 | Threads: 0 | Uptime: 0s | CPU Time: 0s"));
        Assert.That(result, Does.Contain("Memory: WS: 0 B | Priv: 0 B | Paged: 0 B | Virt: 0 B"));
        Assert.That(result, Does.Contain("GC: Heap: 0 B | Allocated: 0 B | Gen 0/1/2: 0/0/0"));
        Assert.That(result, Does.Not.Contain("Peak"));
        Assert.That(result, Does.Not.Contain("Available:"));
    }

    [Test]
    public void Test_ToString_ShouldFormatHoursUptimeAndCpuTime()
    {
        var systemInfo = new SystemInfo
        {
            Uptime = new TimeSpan(3, 15, 30), // 3h 15m 30s
            CpuTime = new TimeSpan(1, 10, 5) // 1h 10m 5s
        };

        var result = systemInfo.ToString();

        Assert.That(result, Does.Contain("Uptime: 3h 15m 30s"));
        Assert.That(result, Does.Contain("CPU Time: 1h 10m 5s"));
    }

    [Test]
    public void Test_ToString_ShouldFormatMinutesUptimeAndCpuTime()
    {
        var systemInfo = new SystemInfo
        {
            Uptime = TimeSpan.FromMinutes(4.5), // 4m 30s
            CpuTime = TimeSpan.FromMinutes(2.5) // 2m 30s
        };

        var result = systemInfo.ToString();

        Assert.That(result, Does.Contain("Uptime: 4m 30s"));
        Assert.That(result, Does.Contain("CPU Time: 2m 30s"));
    }
}
