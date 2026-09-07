using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ElsaMina.Core.Services.System;

public class SystemService : ISystemService
{
    public void Sleep(TimeSpan delay) => Thread.Sleep(delay);

    public Task SleepAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
        Task.Delay(delay, cancellationToken);

    public void Kill(int code = 1) => Environment.Exit(code);

    public SystemInfo GetSystemInfo()
    {
        var process = Process.GetCurrentProcess();
        process.Refresh();

        var gcMemoryInfo = GC.GetGCMemoryInfo();

        var cpuTime = TimeSpan.Zero;
        try
        {
            cpuTime = process.TotalProcessorTime;
        }
        catch
        {
            // Restricted platform/environment
        }

        var uptime = TimeSpan.Zero;
        try
        {
            uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
        }
        catch
        {
            // Restricted platform/environment
        }

        var threadCount = 0;
        try
        {
            threadCount = process.Threads.Count;
        }
        catch
        {
            // Restricted platform/environment
        }

        var peakWorkingSet = process.WorkingSet64;
        try
        {
            peakWorkingSet = Math.Max(peakWorkingSet, process.PeakWorkingSet64);
        }
        catch
        {
            // Restricted platform/environment
        }

        var peakPagedMemory = process.PagedMemorySize64;
        try
        {
            peakPagedMemory = Math.Max(peakPagedMemory, process.PeakPagedMemorySize64);
        }
        catch
        {
            // Restricted platform/environment
        }

        var peakVirtualMemory = process.VirtualMemorySize64;
        try
        {
            peakVirtualMemory = Math.Max(peakVirtualMemory, process.PeakVirtualMemorySize64);
        }
        catch
        {
            // Restricted platform/environment
        }

        return new SystemInfo
        {
            // OS & Runtime
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
            OsDescription = RuntimeInformation.OSDescription,
            OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            ProcessId = Environment.ProcessId,
            Uptime = uptime,
            CpuTime = cpuTime,
            ThreadCount = threadCount,

            // Process Memory
            WorkingSet = process.WorkingSet64,
            PeakWorkingSet = peakWorkingSet,
            PrivateMemory = process.PrivateMemorySize64,
            VirtualMemory = process.VirtualMemorySize64,
            PeakVirtualMemory = peakVirtualMemory,
            PagedMemory = process.PagedMemorySize64,
            PeakPagedMemory = peakPagedMemory,

            // GC / Managed Memory
            GcTotalMemory = GC.GetTotalMemory(forceFullCollection: false),
            GcTotalAllocatedMemory = GC.GetTotalAllocatedBytes(),
            TotalAvailableMemory = gcMemoryInfo.TotalAvailableMemoryBytes,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }
}