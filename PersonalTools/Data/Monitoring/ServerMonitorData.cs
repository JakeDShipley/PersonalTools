using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PersonalTools.Data.Monitoring;

public interface IServerMonitorData
{
    ServerMonitorReading GetReading();
}

public sealed record ServerMonitorReading(
    double? CpuUsagePercent,
    double? MemoryUsagePercent,
    double? StorageUsagePercent,
    long ApplicationMemoryBytes,
    long ManagedMemoryBytes,
    long ApplicationUptimeSeconds);

public sealed class ServerMonitorData : IServerMonitorData
{
    private readonly IWebHostEnvironment _environment;
    private readonly object _cpuLock = new();
    private ulong _previousCpuTotal;
    private ulong _previousCpuIdle;
    private bool _hasCpuSample;

    public ServerMonitorData(IWebHostEnvironment environment) => _environment = environment;

    public ServerMonitorReading GetReading()
    {
        using Process process = Process.GetCurrentProcess();

        return new ServerMonitorReading(
            ReadCpuUsage(),
            ReadMemoryUsage(),
            ReadStorageUsage(),
            process.WorkingSet64,
            GC.GetTotalMemory(false),
            Math.Max(0, (long)(DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds));
    }

    private double? ReadCpuUsage()
    {
        (ulong Idle, ulong Total)? sample = OperatingSystem.IsLinux()
            ? ReadLinuxCpuTimes()
            : OperatingSystem.IsWindows() ? ReadWindowsCpuTimes() : null;

        if (sample is null) return null;

        lock (_cpuLock)
        {
            ulong idle = sample.Value.Idle;
            ulong total = sample.Value.Total;

            if (!_hasCpuSample || total <= _previousCpuTotal)
            {
                _previousCpuIdle = idle;
                _previousCpuTotal = total;
                _hasCpuSample = true;
                return null;
            }

            ulong totalDelta = total - _previousCpuTotal;
            ulong idleDelta = idle - _previousCpuIdle;
            _previousCpuIdle = idle;
            _previousCpuTotal = total;

            return totalDelta == 0 ? null : ClampPercent((totalDelta - Math.Min(idleDelta, totalDelta)) * 100d / totalDelta);
        }
    }

    private static (ulong Idle, ulong Total)? ReadLinuxCpuTimes()
    {
        if (!File.Exists("/proc/stat")) return null;
        string? line = File.ReadLines("/proc/stat").FirstOrDefault();
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("cpu ", StringComparison.Ordinal)) return null;

        ulong[] values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(value => ulong.TryParse(value, out ulong parsed) ? parsed : 0)
            .ToArray();

        if (values.Length < 4) return null;
        ulong idle = values[3] + (values.Length > 4 ? values[4] : 0);
        return (idle, values.Aggregate(0UL, (sum, value) => sum + value));
    }

    private static (ulong Idle, ulong Total)? ReadWindowsCpuTimes()
    {
        if (!GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user)) return null;
        ulong idleValue = idle.ToUInt64();
        return (idleValue, kernel.ToUInt64() + user.ToUInt64());
    }

    private static double? ReadMemoryUsage()
    {
        if (OperatingSystem.IsLinux() && File.Exists("/proc/meminfo"))
        {
            Dictionary<string, long> values = File.ReadLines("/proc/meminfo")
                .Select(line => line.Split(':', 2))
                .Where(parts => parts.Length == 2 && long.TryParse(parts[1].Trim().Split(' ')[0], out _))
                .ToDictionary(parts => parts[0], parts => long.Parse(parts[1].Trim().Split(' ')[0]));

            if (values.TryGetValue("MemTotal", out long total) && values.TryGetValue("MemAvailable", out long available) && total > 0)
                return ClampPercent((total - available) * 100d / total);
        }

        if (OperatingSystem.IsWindows())
        {
            MemoryStatus status = new() { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
            if (GlobalMemoryStatusEx(ref status) && status.TotalPhysical > 0)
                return ClampPercent((status.TotalPhysical - status.AvailablePhysical) * 100d / status.TotalPhysical);
        }

        return null;
    }

    private double? ReadStorageUsage()
    {
        string? root = Path.GetPathRoot(_environment.ContentRootPath);
        if (string.IsNullOrWhiteSpace(root)) return null;

        DriveInfo drive = new(root);
        if (!drive.IsReady || drive.TotalSize <= 0) return null;
        return ClampPercent((drive.TotalSize - drive.AvailableFreeSpace) * 100d / drive.TotalSize);
    }

    private static double ClampPercent(double value) => Math.Round(Math.Clamp(value, 0, 100), 1);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;
        public readonly ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}
