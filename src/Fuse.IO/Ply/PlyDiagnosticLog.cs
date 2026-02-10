namespace Fuse.IO.Ply;

/// <summary>
/// Shared file-based diagnostic logger for PLY node trigger debugging.
/// Writes to Desktop\ply_diagnostic.log. Enable via Debug=true on any PLY node.
/// Thread-safe, auto-flushes, includes timestamps and instance IDs.
/// </summary>
internal static class PlyDiagnosticLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "ply_diagnostic.log");

    private static readonly object Lock = new();
    private static StreamWriter? _writer;
    private static bool _initialized;

    /// <summary>
    /// Write a diagnostic log line with timestamp and caller info.
    /// </summary>
    public static void Write(string nodeName, int instanceId, string message)
    {
        lock (Lock)
        {
            EnsureInitialized();
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            _writer!.WriteLine($"[{timestamp}] [{nodeName}#{instanceId:X4}] {message}");
            _writer.Flush();
        }
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
    public static int GetInstanceId(object obj) => obj.GetHashCode() & 0xFFFF;
}
