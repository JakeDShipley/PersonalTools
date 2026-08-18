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
    double? ApplicationCpuUsagePercent,
    long ApplicationMemoryBytes,
    long ManagedMemoryBytes,
    double? ApplicationMemorySharePercent,
    int ProcessThreadCount,
    int AvailableProcessorCount,
    long ApplicationUptimeSeconds);

public sealed class ServerMonitorData : IServerMonitorData
{
    private readonly IWebHostEnvironment _environment;

    private readonly object _cpuLock = new();
    private ulong _previousCpuTotal;
    private ulong _previousCpuIdle;
    private bool _hasCpuSample;

    private readonly object _applicationCpuLock = new();
    private TimeSpan _previousApplicationCpuTime;
    private long _previousApplicationCpuTimestamp;
    private bool _hasApplicationCpuSample;

    public ServerMonitorData(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public ServerMonitorReading GetReading()
    {
        using Process process = Process.GetCurrentProcess();
        process.Refresh();

        MemoryReading memory = ReadMemoryUsage();

        long applicationMemoryBytes = process.WorkingSet64;
        double? applicationMemorySharePercent = memory.TotalPhysicalBytes > 0
            ? ClampPercent(applicationMemoryBytes * 100d / memory.TotalPhysicalBytes)
            : null;

        return new ServerMonitorReading(
            ReadCpuUsage(),
            memory.UsagePercent,
            ReadStorageUsage(),
            ReadApplicationCpuUsage(process),
            applicationMemoryBytes,
            GC.GetTotalMemory(false),
            applicationMemorySharePercent,
            process.Threads.Count,
            Environment.ProcessorCount,
            Math.Max(0, (long)(DateTime.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds));
    }

    private double? ReadCpuUsage()
    {
        (ulong Idle, ulong Total)? sample = OperatingSystem.IsLinux()
            ? ReadLinuxCpuTimes()
            : OperatingSystem.IsWindows()
                ? ReadWindowsCpuTimes()
                : null;

        if (sample is null)
        {
            return null;
        }

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

            if (totalDelta == 0)
            {
                return null;
            }

            return ClampPercent((totalDelta - Math.Min(idleDelta, totalDelta)) * 100d / totalDelta);
        }
    }

    private double? ReadApplicationCpuUsage(Process process)
    {
        TimeSpan processorTime = process.TotalProcessorTime;
        long timestamp = Stopwatch.GetTimestamp();

        lock (_applicationCpuLock)
        {
            if (!_hasApplicationCpuSample)
            {
                _previousApplicationCpuTime = processorTime;
                _previousApplicationCpuTimestamp = timestamp;
                _hasApplicationCpuSample = true;

                return null;
            }

            TimeSpan elapsed = Stopwatch.GetElapsedTime(_previousApplicationCpuTimestamp, timestamp);
            TimeSpan processorDelta = processorTime - _previousApplicationCpuTime;

            if (elapsed.TotalMilliseconds < 250)
            {
                return null;
            }

            _previousApplicationCpuTime = processorTime;
            _previousApplicationCpuTimestamp = timestamp;

            if (processorDelta < TimeSpan.Zero)
            {
                return null;
            }

            double availableProcessorTime = elapsed.TotalMilliseconds * Math.Max(1, Environment.ProcessorCount);

            return ClampPercent(processorDelta.TotalMilliseconds * 100d / availableProcessorTime);
        }
    }

    private static (ulong Idle, ulong Total)? ReadLinuxCpuTimes()
    {
        if (!File.Exists("/proc/stat"))
        {
            return null;
        }

        string? line = File.ReadLines("/proc/stat").FirstOrDefault();

        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("cpu ", StringComparison.Ordinal))
        {
            return null;
        }

        ulong[] values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(value => ulong.TryParse(value, out ulong parsed) ? parsed : 0)
            .ToArray();

        if (values.Length < 4)
        {
            return null;
        }

        ulong idle = values[3] + (values.Length > 4 ? values[4] : 0);

        // Guest CPU time is already included in the user and nice values, so only
        // the first eight /proc/stat CPU fields are included in the total.
        ulong total = values
            .Take(Math.Min(values.Length, 8))
            .Aggregate(0UL, (sum, value) => sum + value);

        return (idle, total);
    }

    private static (ulong Idle, ulong Total)? ReadWindowsCpuTimes()
    {
        if (!GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user))
        {
            return null;
        }

        ulong idleValue = idle.ToUInt64();

        return (idleValue, kernel.ToUInt64() + user.ToUInt64());
    }

    private static MemoryReading ReadMemoryUsage()
    {
        if (OperatingSystem.IsLinux() && File.Exists("/proc/meminfo"))
        {
            Dictionary<string, long> values = File.ReadLines("/proc/meminfo")
                .Select(line => line.Split(':', 2))
                .Where(parts => parts.Length == 2 && long.TryParse(parts[1].Trim().Split(' ')[0], out _))
                .ToDictionary(parts => parts[0], parts => long.Parse(parts[1].Trim().Split(' ')[0]));

            if (values.TryGetValue("MemTotal", out long total) &&
                values.TryGetValue("MemAvailable", out long available) &&
                total > 0)
            {
                return new MemoryReading(
                    ClampPercent((total - available) * 100d / total),
                    total * 1024);
            }
        }

        if (OperatingSystem.IsWindows())
        {
            MemoryStatus status = new()
            {
                Length = (uint)Marshal.SizeOf<MemoryStatus>()
            };

            if (GlobalMemoryStatusEx(ref status) && status.TotalPhysical > 0)
            {
                return new MemoryReading(
                    ClampPercent((status.TotalPhysical - status.AvailablePhysical) * 100d / status.TotalPhysical),
                    (long)status.TotalPhysical);
            }
        }

        return new MemoryReading(null, 0);
    }

    private double? ReadStorageUsage()
    {
        string? root = Path.GetPathRoot(_environment.ContentRootPath);

        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        DriveInfo drive = new(root);

        if (!drive.IsReady || drive.TotalSize <= 0)
        {
            return null;
        }

        return ClampPercent((drive.TotalSize - drive.AvailableFreeSpace) * 100d / drive.TotalSize);
    }

    private static double ClampPercent(double value)
    {
        return Math.Round(Math.Clamp(value, 0, 100), 1);
    }

    private readonly record struct MemoryReading(double? UsagePercent, long TotalPhysicalBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong ToUInt64()
        {
            return ((ulong)High << 32) | Low;
        }
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