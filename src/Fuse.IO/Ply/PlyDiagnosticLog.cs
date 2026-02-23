namespace Fuse.IO.Ply;

/// <summary>
/// Shared file-based diagnostic logger for PLY node trigger debugging.
/// Writes to Desktop\ply_diagnostic.log. Enable via Debug=true on any PLY node.
/// Thread-safe, auto-flushes, includes timestamps and instance IDs.
/// </summary>
internal static class PlyDiagnosticLog
{
    private sealed class LifetimeStats
    {
        public long Created;
        public long Disposed;
    }

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "ply_diagnostic.log");

    private static readonly object Lock = new();
    private static StreamWriter? _writer;
    private static bool _initialized;
    private static long _lastForcedGcTick;
    private static long _totalEstimatedAllocatedBytes;
    private static long _totalEstimatedReleasedBytes;
    private static readonly Dictionary<string, LifetimeStats> LifetimeByNode = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, bool> DebugStateByInstance = new(StringComparer.Ordinal);
    private static readonly HashSet<string> RegisteredInstances = new(StringComparer.Ordinal);
    private static int _enabledInstanceCount;

    /// <summary>
    /// Write a diagnostic log line with timestamp and caller info.
    /// </summary>
    public static void Write(string nodeName, int instanceId, string message)
    {
        if (!IsEnabledForInstance(nodeName, instanceId)) return;
        lock (Lock)
        {
            if (!IsEnabledForInstance(nodeName, instanceId)) return;
            EnsureInitialized();
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            _writer!.WriteLine($"[{timestamp}] [{nodeName}#{instanceId:X4}] {message}");
            _writer.Flush();
        }
    }

    public static string GetMemorySnapshot()
    {
        try
        {
            using var p = System.Diagnostics.Process.GetCurrentProcess();
            var managedMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            var privateMB = p.PrivateMemorySize64 / (1024.0 * 1024.0);
            var workingMB = p.WorkingSet64 / (1024.0 * 1024.0);
            return $"Managed={managedMB:F1}MB Private={privateMB:F1}MB WorkingSet={workingMB:F1}MB";
        }
        catch
        {
            return "Managed=<n/a> Private=<n/a> WorkingSet=<n/a>";
        }
    }

    public static void TryForceFullGc(string reason, int minIntervalMs = 1500)
    {
        if (!IsEnabledForAnyInstance()) return;
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastForcedGcTick);
        if (now - last < minIntervalMs) return;
        Interlocked.Exchange(ref _lastForcedGcTick, now);

        try
        {
            var beforeManaged = GC.GetTotalMemory(false);
            long beforePrivate = 0;
            long beforeWorking = 0;
            try
            {
                using var pBefore = System.Diagnostics.Process.GetCurrentProcess();
                beforePrivate = pBefore.PrivateMemorySize64;
                beforeWorking = pBefore.WorkingSet64;
            }
            catch
            {
                // ignore snapshot failure
            }

            System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
                System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);

            var afterManaged = GC.GetTotalMemory(false);
            long afterPrivate = 0;
            long afterWorking = 0;
            try
            {
                using var pAfter = System.Diagnostics.Process.GetCurrentProcess();
                afterPrivate = pAfter.PrivateMemorySize64;
                afterWorking = pAfter.WorkingSet64;
            }
            catch
            {
                // ignore snapshot failure
            }

            var dManaged = (afterManaged - beforeManaged) / (1024.0 * 1024.0);
            var dPrivate = (afterPrivate - beforePrivate) / (1024.0 * 1024.0);
            var dWorking = (afterWorking - beforeWorking) / (1024.0 * 1024.0);

            Write("GC", 0,
                $"Forced GC reason={reason} deltaManaged={dManaged:F1}MB deltaPrivate={dPrivate:F1}MB deltaWorkingSet={dWorking:F1}MB {GetMemorySnapshot()}");
            Write("GC", 0, GetAllocationSummary());
        }
        catch
        {
            // best effort only
        }
    }

    public static void TrackEstimatedAllocation(string nodeName, int instanceId, long bytes, string reason)
    {
        if (!IsEnabledForInstance(nodeName, instanceId)) return;
        if (bytes <= 0) return;
        var total = Interlocked.Add(ref _totalEstimatedAllocatedBytes, bytes);
        Write(nodeName, instanceId,
            $"AllocEstimate reason={reason} bytes={bytes} ({bytes / (1024.0 * 1024.0):F1}MB) totalAllocated={total / (1024.0 * 1024.0):F1}MB {GetMemorySnapshot()}");
    }

    public static void TrackEstimatedRelease(string nodeName, int instanceId, long bytes, string reason)
    {
        if (!IsEnabledForInstance(nodeName, instanceId)) return;
        if (bytes <= 0) return;
        var total = Interlocked.Add(ref _totalEstimatedReleasedBytes, bytes);
        Write(nodeName, instanceId,
            $"ReleaseEstimate reason={reason} bytes={bytes} ({bytes / (1024.0 * 1024.0):F1}MB) totalReleased={total / (1024.0 * 1024.0):F1}MB {GetMemorySnapshot()}");
    }

    public static void RegisterInstance(string nodeName, int instanceId, string? callsite = null)
    {
        if (!IsEnabledForInstance(nodeName, instanceId)) return;
        lock (Lock)
        {
            if (!IsEnabledForInstance(nodeName, instanceId)) return;
            EnsureInitialized();
            var stats = GetOrCreateLifetimeStats(nodeName);
            stats.Created++;
            RegisteredInstances.Add(GetInstanceKey(nodeName, instanceId));
            var live = Math.Max(0, stats.Created - stats.Disposed);
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            _writer!.WriteLine(
                $"[{timestamp}] [{nodeName}#{instanceId:X4}] Lifetime created={stats.Created} disposed={stats.Disposed} liveEstimate={live}");
            if (!string.IsNullOrWhiteSpace(callsite))
                _writer.WriteLine($"[{timestamp}] [{nodeName}#{instanceId:X4}] CreatedAt {callsite}");
            _writer.Flush();
        }
    }

    public static void MarkDisposed(string nodeName, int instanceId)
    {
        var key = GetInstanceKey(nodeName, instanceId);
        if (!HasRegisteredInstance(key)) return;
        lock (Lock)
        {
            if (!HasRegisteredInstance(key)) return;
            EnsureInitialized();
            var stats = GetOrCreateLifetimeStats(nodeName);
            stats.Disposed++;
            var live = Math.Max(0, stats.Created - stats.Disposed);
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            _writer!.WriteLine(
                $"[{timestamp}] [{nodeName}#{instanceId:X4}] Lifetime created={stats.Created} disposed={stats.Disposed} liveEstimate={live}");
            _writer.Flush();
        }
    }

    public static string GetAllocationSummary()
    {
        if (!IsEnabledForAnyInstance()) return "AllocationSummary disabled";
        var allocated = Interlocked.Read(ref _totalEstimatedAllocatedBytes);
        var released = Interlocked.Read(ref _totalEstimatedReleasedBytes);
        var retained = Math.Max(0, allocated - released);
        var lifetime = GetLifetimeSummary();
        return
            $"AllocationSummary allocated={allocated / (1024.0 * 1024.0):F1}MB released={released / (1024.0 * 1024.0):F1}MB retainedEstimate={retained / (1024.0 * 1024.0):F1}MB lifetimes={lifetime}";
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _writer = new StreamWriter(LogPath, append: true) { AutoFlush = true };
        _writer.WriteLine();
        _writer.WriteLine($"===== SESSION START {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
        _writer.WriteLine();
        _initialized = true;
    }

    /// <summary>
    /// Generates a short instance ID from the object's hash code, useful to
    /// distinguish different node instances (e.g., after recompile).
    /// </summary>
    public static int GetInstanceId(object obj) => obj.GetHashCode() & 0x7FFFFFFF;

    public static void SetDebugEnabled(string nodeName, int instanceId, bool enabled)
    {
        var key = GetInstanceKey(nodeName, instanceId);
        lock (Lock)
        {
            var had = DebugStateByInstance.TryGetValue(key, out var previous) && previous;
            DebugStateByInstance[key] = enabled;
            if (!had && enabled)
            {
                _enabledInstanceCount++;
                return;
            }

            if (had && !enabled && _enabledInstanceCount > 0)
            {
                _enabledInstanceCount--;
                if (_enabledInstanceCount == 0)
                {
                    _writer?.Flush();
                    _writer?.Dispose();
                    _writer = null;
                    _initialized = false;
                }
            }
        }
    }

    private static LifetimeStats GetOrCreateLifetimeStats(string nodeName)
    {
        if (LifetimeByNode.TryGetValue(nodeName, out var stats))
            return stats;
        stats = new LifetimeStats();
        LifetimeByNode[nodeName] = stats;
        return stats;
    }

    private static string GetLifetimeSummary()
    {
        lock (Lock)
        {
            if (LifetimeByNode.Count == 0) return "none";
            var parts = new List<string>(LifetimeByNode.Count);
            foreach (var kv in LifetimeByNode)
            {
                var live = Math.Max(0, kv.Value.Created - kv.Value.Disposed);
                parts.Add($"{kv.Key}:c{kv.Value.Created}/d{kv.Value.Disposed}/l{live}");
            }

            return string.Join(",", parts);
        }
    }

    private static bool IsEnabledForAnyInstance() => Volatile.Read(ref _enabledInstanceCount) > 0;

    private static string GetInstanceKey(string nodeName, int instanceId) => $"{nodeName}#{instanceId:X8}";

    private static bool IsEnabledForInstance(string nodeName, int instanceId)
    {
        var key = GetInstanceKey(nodeName, instanceId);
        lock (Lock)
            return DebugStateByInstance.TryGetValue(key, out var enabled) && enabled;
    }

    private static bool HasRegisteredInstance(string instanceKey)
    {
        lock (Lock)
            return RegisteredInstances.Contains(instanceKey);
    }
}
