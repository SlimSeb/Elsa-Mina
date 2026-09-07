using System.Globalization;
using ElsaMina.Core.Utils;

namespace ElsaMina.Core.Services.System;

public class SystemInfo
{
    // OS & Runtime
    public string FrameworkDescription { get; set; }
    public string RuntimeIdentifier { get; set; }
    public string OsDescription { get; set; }
    public string OsArchitecture { get; set; }
    public string ProcessArchitecture { get; set; }
    public int ProcessorCount { get; set; }
    public int ProcessId { get; set; }
    public TimeSpan Uptime { get; set; }
    public TimeSpan CpuTime { get; set; }
    public int ThreadCount { get; set; }

    // Process Memory
    public long WorkingSet { get; set; }
    public long PeakWorkingSet { get; set; }
    public long PrivateMemory { get; set; }
    public long VirtualMemory { get; set; }
    public long PeakVirtualMemory { get; set; }
    public long PagedMemory { get; set; }
    public long PeakPagedMemory { get; set; }

    // GC / Managed Memory
    public long GcTotalMemory { get; set; }
    public long GcTotalAllocatedMemory { get; set; }
    public long TotalAvailableMemory { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }

    public override string ToString()
    {
        var osPart = !string.IsNullOrWhiteSpace(OsDescription)
            ? $"{OsDescription} ({OsArchitecture ?? "Unknown"})"
            : "Unknown OS";

        var runtimePart = !string.IsNullOrWhiteSpace(FrameworkDescription)
            ? $"{FrameworkDescription} ({RuntimeIdentifier ?? "Unknown"})"
            : "Unknown Runtime";

        var peakWs = PeakWorkingSet > 0
            ? $" (Peak: {PeakWorkingSet.ToReadableDataSize()})"
            : string.Empty;

        var availableMem = TotalAvailableMemory > 0
            ? $" | Available: {TotalAvailableMemory.ToReadableDataSize()}"
            : string.Empty;

        var line1 = $"OS: {osPart} | Runtime: {runtimePart}";
        var line2 = $"CPU: {ProcessorCount} cores | PID: {ProcessId} | Threads: {ThreadCount} | Uptime: {FormatTimeSpan(Uptime)} | CPU Time: {FormatCpuTime(CpuTime)}";
        var line3 = $"Memory: WS: {WorkingSet.ToReadableDataSize()}{peakWs} | Priv: {PrivateMemory.ToReadableDataSize()} | Paged: {PagedMemory.ToReadableDataSize()} | Virt: {VirtualMemory.ToReadableDataSize()}";
        var line4 = $"GC: Heap: {GcTotalMemory.ToReadableDataSize()} | Allocated: {GcTotalAllocatedMemory.ToReadableDataSize()} | Gen 0/1/2: {Gen0Collections}/{Gen1Collections}/{Gen2Collections}{availableMem}";

        return $"{line1}\n{line2}\n{line3}\n{line4}";
    }

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan <= TimeSpan.Zero)
        {
            return "0s";
        }

        if (timeSpan.TotalDays >= 1)
        {
            return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        }

        if (timeSpan.TotalHours >= 1)
        {
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        }

        if (timeSpan.TotalMinutes >= 1)
        {
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        }

        return $"{timeSpan.Seconds}s";
    }

    private static string FormatCpuTime(TimeSpan cpuTime)
    {
        if (cpuTime <= TimeSpan.Zero)
        {
            return "0s";
        }

        if (cpuTime.TotalHours >= 1)
        {
            return $"{(int)cpuTime.TotalHours}h {cpuTime.Minutes}m {cpuTime.Seconds}s";
        }

        if (cpuTime.TotalMinutes >= 1)
        {
            return $"{cpuTime.Minutes}m {cpuTime.Seconds}s";
        }

        return $"{cpuTime.TotalSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s";
    }
}