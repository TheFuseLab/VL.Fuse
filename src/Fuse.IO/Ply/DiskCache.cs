using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

#pragma warning disable CS1591

namespace Fuse.IO.Ply;

public static class DiskCachePaths
{
    public static string BasePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VL.Fuse", "OctreePlyCache");

    public static string GetNamespacePath(string cacheNamespace)
    {
        var path = Path.Combine(BasePath, Sanitize(cacheNamespace));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }
}

public sealed class DiskCache
{
    internal static bool s_loggedPlySettings;
    internal static bool s_loggedOctreeSettings;

    // DiskCache-level helpers (accessible from all serializers)
    internal static async Task ReadAtExactlyAsync(SafeFileHandle handle, Memory<byte> destination, long fileOffset,
        CancellationToken ct)
    {
        var read = 0;
        while (read < destination.Length)
        {
            var r = await RandomAccess.ReadAsync(handle, destination.Slice(read), fileOffset + read, ct);
            if (r == 0) break;
            read += r;
        }
    }

    internal static async Task ReadFileParallelRandomAccessAsync(SafeFileHandle handle, byte[] destination,
        int chunkSize, int degree, CancellationToken ct, string label = "")
    {
        // NOTE: This helper is kept for compatibility but is no longer used by the Octree serializer.
        if (destination.Length == 0) return;
        var length = destination.Length;
        var chunks = (int)Math.Ceiling(length / (double)chunkSize);
        var tasks = new List<Task>(chunks);
        var sw = Stopwatch.StartNew();

        // Simple throttling by batching at most "degree" chunks at once
        for (var i = 0; i < chunks; i++)
        {
            var start = i * chunkSize;
            var size = Math.Min(chunkSize, length - start);

            tasks.Add(ReadChunkAsync(handle, destination, start, size, ct));

            if (tasks.Count == degree)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
            }
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);

        sw.Stop();
        try
        {
            if (!string.IsNullOrEmpty(label))
            {
                var mb = destination.LongLength / (1024.0 * 1024.0);
                var mbps = mb / Math.Max(1e-6, sw.Elapsed.TotalSeconds);
                Console.WriteLine(
                    $"[DiskCache:{label}] Read {mb:F1} MB in {sw.Elapsed.TotalMilliseconds:F0} ms ({mbps:F1} MB/s)");
            }
        }
        catch
        {
        }
    }

    private static async Task ReadChunkAsync(SafeFileHandle handle, byte[] destination, int start, int size,
        CancellationToken ct)
    {
        var mem = destination.AsMemory(start, size);
        var offset = 0;
        while (offset < size)
        {
            var r = await RandomAccess.ReadAsync(handle, mem.Slice(offset), start + offset, ct);
            if (r == 0) break;
            offset += r;
        }
    }

    // Synchronous exact read into Span<byte> (avoids Span across await restriction)
    internal static void ReadAtExactly(SafeFileHandle handle, Span<byte> destination, long fileOffset,
        CancellationToken ct)
    {
        var read = 0;
        while (read < destination.Length)
        {
            ct.ThrowIfCancellationRequested();
            var r = RandomAccess.Read(handle, destination.Slice(read), fileOffset + read);
            if (r == 0) break;
            read += r;
        }
    }

    internal static void TryLogEnvironment(string tag)
    {
        try
        {
            var basePath = DiskCachePaths.BasePath;
            var root = Path.GetPathRoot(basePath) ?? string.Empty;
            var di = string.IsNullOrEmpty(root) ? null : new DriveInfo(root);
            if (di != null)
                Console.WriteLine(
                    $"[DiskCache:{tag}] BasePath={basePath} Drive={di.Name} Free={di.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0):F1}GB");
            else
                Console.WriteLine($"[DiskCache:{tag}] BasePath={basePath}");
        }
        catch
        {
        }
    }

    public static async Task<T> GetOrCreateAsync<T>(string cacheNamespace, string key, ICacheSerializer<T> serializer,
        Func<CancellationToken, Task<T>> buildAsync, CancellationToken ct)
    {
        var entry = GetEntryDir(cacheNamespace, key);
        var manifestPath = Path.Combine(entry, "manifest.json");
        if (File.Exists(manifestPath))
            try
            {
                var man = await ReadManifestAsync(manifestPath, ct);
                if (man != null && man.Key == key && man.PayloadType == serializer.PayloadType)
                {
                    var payload = await serializer.ReadAsync(entry, ct);
                    await Touch(manifestPath, man, ct);
                    return payload;
                }
            }
            catch
            {
            }

        Directory.CreateDirectory(entry);
        var tmpDir = Path.Combine(entry, "tmp");
        if (Directory.Exists(tmpDir))
            try
            {
                Directory.Delete(tmpDir, true);
            }
            catch
            {
            }

        Directory.CreateDirectory(tmpDir);

        var tmpEntryDir = tmpDir;
        var finalEntryDir = entry;

        var tmpManifest = Path.Combine(tmpEntryDir, "manifest.json");

        var payloadBuilt = await buildAsync(ct);
        await serializer.WriteAsync(tmpEntryDir, payloadBuilt, ct);

        var size = await serializer.ComputeSizeAsync(tmpEntryDir, ct);
        var sha = await serializer.ComputeSha256Async(tmpEntryDir, ct);
        var manifest = new Manifest
        {
            Version = "1",
            Key = key,
            PayloadType = serializer.PayloadType,
            Encoding = "raw",
            SizeBytes = size,
            Sha256 = sha,
            CreatedUtc = DateTime.UtcNow,
            LastAccessUtc = DateTime.UtcNow,
            Hits = 0
        };
        await WriteManifestAsync(tmpManifest, manifest, ct);

        await AtomicSwapAsync(tmpEntryDir, finalEntryDir);

        return payloadBuilt;
    }

    public static bool TryGet<T>(string cacheNamespace, string key, ICacheSerializer<T> serializer,
        CancellationToken ct, out Task<T> payloadTask)
    {
        var entry = GetEntryDir(cacheNamespace, key);
        var manifestPath = Path.Combine(entry, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            payloadTask = Task.FromResult(default(T)!);
            return false;
        }

        try
        {
            var man = ReadManifestSync(manifestPath);
            if (man != null && man.Key == key && man.PayloadType == serializer.PayloadType)
            {
                payloadTask = serializer.ReadAsync(entry, ct)!;
                _ = Touch(manifestPath, man, ct);
                return true;
            }
        }
        catch
        {
        }

        payloadTask = Task.FromResult(default(T)!);
        return false;
    }

    public static void Invalidate(string cacheNamespace, string key)
    {
        var entry = GetEntryDir(cacheNamespace, key);
        try
        {
            if (Directory.Exists(entry)) Directory.Delete(entry, true);
        }
        catch
        {
        }
    }

    public static void ClearNamespace(string cacheNamespace)
    {
        var nsPath = DiskCachePaths.GetNamespacePath(cacheNamespace);
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(nsPath))
                try
                {
                    Directory.Delete(dir, true);
                }
                catch
                {
                }
        }
        catch
        {
        }
    }

    private static string GetEntryDir(string cacheNamespace, string key)
    {
        using var sha = SHA256.Create();
        var keyHash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        var nsPath = DiskCachePaths.GetNamespacePath(cacheNamespace);
        var entry = Path.Combine(nsPath, keyHash);
        return entry;
    }

    private static async Task<Manifest?> ReadManifestAsync(string path, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return await JsonSerializer.DeserializeAsync<Manifest>(fs, cancellationToken: ct);
    }

    private static async Task WriteManifestAsync(string path, Manifest manifest, CancellationToken ct)
    {
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await JsonSerializer.SerializeAsync(fs, manifest, cancellationToken: ct);
        await fs.FlushAsync(ct);
    }

    private static Manifest? ReadManifestSync(string path)
    {
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize<Manifest>(json);
        }
        catch
        {
            return null;
        }
    }

    private static async Task Touch(string manifestPath, Manifest man, CancellationToken ct)
    {
        try
        {
            man.LastAccessUtc = DateTime.UtcNow;
            man.Hits++;
            await WriteManifestAsync(manifestPath, man, ct);
        }
        catch
        {
        }
    }

    private static async Task AtomicSwapAsync(string tmpEntryDir, string finalEntryDir)
    {
        var parent = Path.GetDirectoryName(finalEntryDir) ?? Path.GetTempPath();
        var staging = Path.Combine(parent, "staging_" + Guid.NewGuid().ToString("N"));
        Directory.Move(tmpEntryDir, staging);
        if (Directory.Exists(finalEntryDir))
            try
            {
                Directory.Delete(finalEntryDir, true);
            }
            catch
            {
            }

        Directory.Move(staging, finalEntryDir);
        await Task.CompletedTask;
    }

    // Tunables for NVMe throughput
    internal static class ReadSettings
    {
        public static int OctreeChunkSizeBytes { get; set; } =
            64 * 1024 * 1024; // 64 MB (not used in new Octree read, but kept for compatibility)

        public static int OctreeDegree { get; set; } = 8; // kept for compatibility
        public static int PlyDegree { get; set; } = 8; // kept for compatibility (no longer used in PLY read)
    }

    public sealed class Manifest
    {
        public string? Version { get; set; }
        public string? Key { get; set; }
        public string? PayloadType { get; set; }
        public string? Encoding { get; set; }
        public long SizeBytes { get; set; }
        public string? Sha256 { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime LastAccessUtc { get; set; }
        public long Hits { get; set; }
    }

    public interface ICacheSerializer<T>
    {
        string PayloadType { get; }
        Task WriteAsync(string entryDir, T payload, CancellationToken ct);
        Task<T> ReadAsync(string entryDir, CancellationToken ct);
        Task<long> ComputeSizeAsync(string entryDir, CancellationToken ct);
        Task<string> ComputeSha256Async(string entryDir, CancellationToken ct);
    }
}

public sealed class PlyArraysCacheSerializer : DiskCache.ICacheSerializer<Dictionary<string, float[]>>
{
    private const string DataFile = "arrays.bin";
    private const string TocFile = "arrays.toc.json";
    public string PayloadType => "ply_arrays_v1";

    public async Task WriteAsync(string entryDir, Dictionary<string, float[]> payload, CancellationToken ct)
    {
        var toc = new List<TocEntry>();
        var offset = 0L;
        foreach (var kv in payload)
        {
            var bytes = kv.Value.LongLength * sizeof(float);
            toc.Add(new TocEntry { Name = kv.Key, Offset = offset, LengthFloats = kv.Value.LongLength });
            offset += bytes;
        }

        await using (var fs = new FileStream(Path.Combine(entryDir ?? string.Empty, DataFile), FileMode.Create,
                         FileAccess.Write, FileShare.None, 1024 * 1024, true))
        {
            foreach (var kv in payload)
            {
                var arr = kv.Value;
                var buffer = new byte[arr.LongLength * sizeof(float)];
                Buffer.BlockCopy(arr, 0, buffer, 0, buffer.Length);
                await fs.WriteAsync(buffer, 0, buffer.Length, ct);
            }

            await fs.FlushAsync(ct);
        }

        await using (var fs = new FileStream(Path.Combine(entryDir ?? string.Empty, TocFile), FileMode.Create,
                         FileAccess.Write, FileShare.None, 4096, true))
        {
            await JsonSerializer.SerializeAsync(fs, toc, cancellationToken: ct);
            await fs.FlushAsync(ct);
        }
    }

    public async Task<Dictionary<string, float[]>> ReadAsync(string entryDir, CancellationToken ct)
    {
        var swTotal = Stopwatch.StartNew();

        // --- TOC (sync, tiny file) ---
        List<TocEntry>? toc;
        var tocPath = Path.Combine(entryDir ?? string.Empty, TocFile);
        using (var fs = new FileStream(tocPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, false))
        {
            toc = JsonSerializer.Deserialize<List<TocEntry>>(fs);
        }

        toc ??= new List<TocEntry>();

        var dataPath = Path.Combine(entryDir ?? string.Empty, DataFile);
        var fi = new FileInfo(dataPath);
        var length = fi.Exists ? fi.Length : 0L;
        if (length <= 0)
            return new Dictionary<string, float[]>(0, StringComparer.OrdinalIgnoreCase);

        // Decide if we can safely use the single-big-buffer fast path
        var canUseSingleBuffer = length <= int.MaxValue;
        if (canUseSingleBuffer)
            foreach (var entry in toc)
            {
                var fieldBytes = entry.LengthFloats * sizeof(float);
                var end = entry.Offset + fieldBytes;
                if (end > int.MaxValue)
                {
                    canUseSingleBuffer = false;
                    break;
                }
            }

        if (!DiskCache.s_loggedPlySettings)
        {
            DiskCache.s_loggedPlySettings = true;
            DiskCache.TryLogEnvironment("PLY");
            try
            {
                var top = toc
                    .Select(t => new
                        { Name = t.Name ?? "values", MB = t.LengthFloats * sizeof(float) / (1024.0 * 1024.0) })
                    .OrderByDescending(x => x.MB)
                    .Take(3)
                    .ToArray();
                Console.WriteLine(
                    $"[DiskCache:PLY] Top fields by size: {string.Join(", ", top.Select(x => x.Name + ":" + x.MB.ToString("F1") + "MB"))}");
            }
            catch
            {
            }
        }

        var result = new Dictionary<string, float[]>(toc.Count, StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;

        if (canUseSingleBuffer)
        {
            // -----------------------------
            // FAST PATH: single big buffer
            // -----------------------------
            var swRead = Stopwatch.StartNew();
            var dataBuffer = GC.AllocateUninitializedArray<byte>(checked((int)length));
            using (var handle = File.OpenHandle(
                       dataPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       FileOptions.SequentialScan))
            {
                DiskCache.ReadAtExactly(handle, dataBuffer.AsSpan(), 0, ct);
            }

            swRead.Stop();

            foreach (var entry in toc)
            {
                var name = entry.Name ?? "values";
                var byteLength = checked((int)(entry.LengthFloats * sizeof(float)));
                var byteOffset = checked((int)entry.Offset);

                var floats = new float[checked((int)entry.LengthFloats)];
                Buffer.BlockCopy(
                    dataBuffer,
                    byteOffset,
                    floats,
                    0,
                    byteLength);

                result[name] = floats;
                totalBytes += byteLength;
            }

            swTotal.Stop();
            try
            {
                var mb = totalBytes / (1024.0 * 1024.0);
                var mbpsDisk = mb / Math.Max(1e-6, swRead.Elapsed.TotalSeconds);
                var mbpsTotal = mb / Math.Max(1e-6, swTotal.Elapsed.TotalSeconds);
                Console.WriteLine(
                    $"[DiskCache:PLY] FAST single-buffer Read+decode {mb:F1} MB " +
                    $"Disk {swRead.ElapsedMilliseconds:F0} ms ({mbpsDisk:F1} MB/s), " +
                    $"Total {swTotal.Elapsed.TotalMilliseconds:F0} ms ({mbpsTotal:F1} MB/s)");
            }
            catch
            {
            }
        }
        else
        {
            // ---------------------------------------------------
            // FALLBACK: per-field streaming from one file handle
            //           but now DIRECTLY into float[]
            //           (no extra byte[] + BlockCopy)
            // ---------------------------------------------------
            Console.WriteLine(
                "[DiskCache:PLY] arrays.bin > 2GB or offsets exceed Int32; using per-field streaming fallback (direct-to-float).");

            using var handle = File.OpenHandle(
                dataPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileOptions.SequentialScan);

            foreach (var entry in toc)
            {
                ct.ThrowIfCancellationRequested();

                var name = entry.Name ?? "values";

                // Each individual field must still fit into a single array
                var floatCount = checked((int)entry.LengthFloats);
                var floats = new float[floatCount];

                // Interpret float[] as bytes so we can read directly into it
                var byteSpan = MemoryMarshal.AsBytes(floats.AsSpan());
                var byteLength = byteSpan.Length;

                // Synchronous sequential read into float[] memory
                DiskCache.ReadAtExactly(handle, byteSpan, entry.Offset, ct);

                result[name] = floats;
                totalBytes += byteLength;
            }

            swTotal.Stop();
            try
            {
                var mb = totalBytes / (1024.0 * 1024.0);
                var mbpsTotal = mb / Math.Max(1e-6, swTotal.Elapsed.TotalSeconds);
                Console.WriteLine(
                    $"[DiskCache:PLY] FALLBACK direct-to-float streaming {mb:F1} MB " +
                    $"Total {swTotal.Elapsed.TotalMilliseconds:F0} ms ({mbpsTotal:F1} MB/s)");
            }
            catch
            {
            }
        }

        await Task.CompletedTask; // keep async signature
        return result;
    }


    public async Task<long> ComputeSizeAsync(string entryDir, CancellationToken ct)
    {
        var fi = new FileInfo(Path.Combine(entryDir ?? string.Empty, DataFile));
        return await Task.FromResult(fi.Exists ? fi.Length : 0L);
    }

    public async Task<string> ComputeSha256Async(string entryDir, CancellationToken ct)
    {
        var path = Path.Combine(entryDir ?? string.Empty, DataFile);
        if (!File.Exists(path)) return string.Empty;
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ReadFloatArray(Stream data, float[] destination, CancellationToken ct)
    {
        // kept for compatibility, no longer used
        var bytesSpan = MemoryMarshal.AsBytes(destination.AsSpan());
        var read = 0;
        while (read < bytesSpan.Length)
        {
            var r = data.Read(bytesSpan.Slice(read));
            if (r == 0) break;
            read += r;
            if (ct.IsCancellationRequested) break;
        }
    }

    private sealed class TocEntry
    {
        public string? Name { get; set; }
        public long Offset { get; set; }
        public long LengthFloats { get; set; }
    }
}

public sealed class OctreeCacheSerializer : DiskCache.ICacheSerializer<OctreeCacheSerializer.Payload>
{
    private const string Nodes = "nodes.bin";
    private const string Indices = "indices.bin";
    private const string Meta = "meta.json";

    public string PayloadType => "octree_buffers_v1";

    public async Task WriteAsync(string entryDir, Payload payload, CancellationToken ct)
    {
        await File.WriteAllBytesAsync(Path.Combine(entryDir ?? string.Empty, Nodes),
            payload.NodeBuffer ?? Array.Empty<byte>(), ct);
        await File.WriteAllBytesAsync(Path.Combine(entryDir ?? string.Empty, Indices),
            payload.IndexBuffer ?? Array.Empty<byte>(), ct);
        await using var fs = new FileStream(Path.Combine(entryDir ?? string.Empty, Meta), FileMode.Create,
            FileAccess.Write, FileShare.None, 4096, true);
        await JsonSerializer.SerializeAsync(fs, new MetaInfo
        {
            NodeCount = payload.NodeCount,
            IndexCount = payload.IndexCount,
            TotalMemory = payload.TotalMemory,
            HasPrecomputedLODs = payload.HasPrecomputedLODs
        }, cancellationToken: ct);
        await fs.FlushAsync(ct);
    }

    public async Task<Payload> ReadAsync(string entryDir, CancellationToken ct)
    {
        var nodesPath = Path.Combine(entryDir ?? string.Empty, Nodes);
        var indicesPath = Path.Combine(entryDir ?? string.Empty, Indices);

        var fiNodes = new FileInfo(nodesPath);
        var fiIndices = new FileInfo(indicesPath);

        var nodeBuffer = GC.AllocateUninitializedArray<byte>(checked((int)(fiNodes.Exists ? fiNodes.Length : 0)));
        var indexBuffer = GC.AllocateUninitializedArray<byte>(checked((int)(fiIndices.Exists ? fiIndices.Length : 0)));

        var sw = Stopwatch.StartNew();

        if (!DiskCache.s_loggedOctreeSettings)
        {
            DiskCache.s_loggedOctreeSettings = true;
            DiskCache.TryLogEnvironment("Octree");
            Console.WriteLine("[DiskCache:Octree] Using sequential read");
            Console.WriteLine(
                $"[DiskCache:Octree] Sizes: nodes={nodeBuffer.LongLength / (1024.0 * 1024.0):F1}MB indices={indexBuffer.LongLength / (1024.0 * 1024.0):F1}MB");
        }

        // Sequential read for nodes.bin
        using (var hNodes = File.OpenHandle(
                   nodesPath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read,
                   FileOptions.SequentialScan))
        {
            DiskCache.ReadAtExactly(hNodes, nodeBuffer.AsSpan(), 0, ct);
        }

        // Sequential read for indices.bin
        using (var hIndices = File.OpenHandle(
                   indicesPath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read,
                   FileOptions.SequentialScan))
        {
            DiskCache.ReadAtExactly(hIndices, indexBuffer.AsSpan(), 0, ct);
        }

        sw.Stop();
        try
        {
            var totalMB = (nodeBuffer.LongLength + indexBuffer.LongLength) / (1024.0 * 1024.0);
            var mbps = totalMB / Math.Max(1e-6, sw.Elapsed.TotalSeconds);
            Console.WriteLine(
                $"[DiskCache:Octree] Read {totalMB:F1} MB in {sw.Elapsed.TotalMilliseconds:F0} ms ({mbps:F1} MB/s)");
        }
        catch
        {
        }

        MetaInfo? meta;
        await using (var fs = new FileStream(Path.Combine(entryDir ?? string.Empty, Meta), FileMode.Open,
                         FileAccess.Read, FileShare.Read, 4096, true))
        {
            meta = await JsonSerializer.DeserializeAsync<MetaInfo>(fs, cancellationToken: ct);
        }

        var m = meta ?? new MetaInfo();
        return new Payload(nodeBuffer, indexBuffer, m.NodeCount, m.IndexCount, m.TotalMemory, m.HasPrecomputedLODs);
    }

    public async Task<long> ComputeSizeAsync(string entryDir, CancellationToken ct)
    {
        var fi1 = new FileInfo(Path.Combine(entryDir ?? string.Empty, Nodes));
        var fi2 = new FileInfo(Path.Combine(entryDir ?? string.Empty, Indices));
        return await Task.FromResult((fi1.Exists ? fi1.Length : 0) + (fi2.Exists ? fi2.Length : 0));
    }

    public async Task<string> ComputeSha256Async(string entryDir, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        var path1 = Path.Combine(entryDir ?? string.Empty, Nodes);
        var path2 = Path.Combine(entryDir ?? string.Empty, Indices);
        var hash1 = Array.Empty<byte>();
        var hash2 = Array.Empty<byte>();
        if (File.Exists(path1))
        {
            await using var fs1 = new FileStream(path1, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
                true);
            hash1 = await sha.ComputeHashAsync(fs1, ct);
        }

        if (File.Exists(path2))
        {
            await using var fs2 = new FileStream(path2, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
                true);
            hash2 = await sha.ComputeHashAsync(fs2, ct);
        }

        using var sha2 = SHA256.Create();
        var combined = new byte[hash1.Length + hash2.Length];
        Buffer.BlockCopy(hash1, 0, combined, 0, hash1.Length);
        Buffer.BlockCopy(hash2, 0, combined, hash1.Length, hash2.Length);
        var final = sha2.ComputeHash(combined);
        return Convert.ToHexString(final).ToLowerInvariant();
    }

    public readonly struct Payload
    {
        public Payload(byte[] nodeBuffer, byte[] indexBuffer, int nodeCount, int indexCount, long totalMemory,
            bool hasPrecomputedLODs)
        {
            NodeBuffer = nodeBuffer;
            IndexBuffer = indexBuffer;
            NodeCount = nodeCount;
            IndexCount = indexCount;
            TotalMemory = totalMemory;
            HasPrecomputedLODs = hasPrecomputedLODs;
        }

        public byte[] NodeBuffer { get; }
        public byte[] IndexBuffer { get; }
        public int NodeCount { get; }
        public int IndexCount { get; }
        public long TotalMemory { get; }
        public bool HasPrecomputedLODs { get; }
    }

    private sealed class MetaInfo
    {
        public int NodeCount { get; set; }
        public int IndexCount { get; set; }
        public long TotalMemory { get; set; }
        public bool HasPrecomputedLODs { get; set; }
    }
}

public static class DiskCacheKey
{
    public static string FromFileIdentity(string filePath, string? headerSignature = null)
    {
        var fi = new FileInfo(filePath);
        var sb = new StringBuilder();
        sb.Append("path=").Append(fi.FullName.ToLowerInvariant());
        sb.Append("|len=").Append(fi.Exists ? fi.Length : -1);
        sb.Append("|mtime=").Append(fi.Exists ? fi.LastWriteTimeUtc.Ticks : 0);
        if (!string.IsNullOrEmpty(headerSignature)) sb.Append("|hdr=").Append(headerSignature);
        return sb.ToString();
    }

    public static string ForOctree(string pointCloudKey, int maxDepth, int maxPointsPerLeaf, float subdivisionEpsilon,
        bool optimizeForSpeed, bool enableDetailedValidation)
    {
        var s =
            $"pc={pointCloudKey}|md={maxDepth}|mpl={maxPointsPerLeaf}|eps={subdivisionEpsilon}|opt={optimizeForSpeed}|veri={enableDetailedValidation}";
        return s;
    }
}