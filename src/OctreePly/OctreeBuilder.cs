using System;
using System.Diagnostics;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vector3 = Stride.Core.Mathematics.Vector3;

namespace VL.E57
{
    #pragma warning disable CS1591
    // VVVV Process Node für GPU-Octree-Build + CPU-seitiges Precompute-LOD (Leaves)
    [ProcessNode]
    public class OctreeBuilder
    {
        private Task? _buildTask;
        private bool _isBuilding;
        private bool _lastBuildTrigger;
        private GPUOctree.OctreeProgressInfo? _progressInfo;
        private bool _hasPrecomputedLODs;
        private bool _wasCacheHit;   // NEW: track whether this run used a cache hit

        // ---------------------------
        // Inputs
        // ---------------------------
        public Dictionary<string, float[]> PLYData { private get; set; } = new Dictionary<string, float[]>(0);
        public bool Build { private get; set; }
        public int MaxDepth { private get; set; } = 8;
        public int MaxPointsPerLeaf { private get; set; } = 5000;
        public bool UseDiskCache { private get; set; }
        public bool ForceCacheUpdate { private get; set; }
        public string CacheBasePath { private get; set; } = string.Empty;
        public string PointCloudKey { private get; set; } = string.Empty;
        public bool Debug { private get; set; }

        /// <summary>
        /// Aktiviert die CPU-seitige Progressive-LOD-Permutation für Leaf-Nodes.
        /// </summary>
        public bool EnablePrecomputedLeafLODs { private get; set; } = true;

        /// <summary>
        /// Grobe Zielanzahl Zellen auf längster Kante pro Leaf (steuert die Grob→Fein-Layer).
        /// Größer = mehr Grobstruktur zuerst. 24–48 sind gute Startwerte.
        /// </summary>
        public int LeafTargetCellsOnLongestAxis { private get; set; } = 32;

        // ---------------------------
        // Outputs – Progress/Status
        // ---------------------------
        public float Progress { get; private set; }
        public string Status { get; private set; } = "Ready";
        public string StageName { get; private set; } = "";
        public int TotalPoints { get; private set; }
        public bool IsBuilding { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool HasError { get; private set; }
        public string ErrorMessage { get; private set; } = "";

        // ---------------------------
        // Outputs – Resultate
        // ---------------------------
        public byte[] NodeBufferData { get; private set; } = Array.Empty<byte>();
        public byte[] IndexBufferData { get; private set; } = Array.Empty<byte>();
        public int NodeCount { get; private set; }
        public int IndexCount { get; private set; }
        public int NodesProcessed { get; private set; }
        public long TotalMemoryUsed { get; private set; }

        // ---------------------------
        // Outputs – Statistik
        // ---------------------------
        public float CompressionRatio { get; private set; }
        public float NodeBufferSizeMB { get; private set; }
        public float IndexBufferSizeMB { get; private set; }
        public float TotalSizeMB { get; private set; }

        // ===========================
        // Lifecycle
        // ===========================
        public void Update()
        {
            // Rising edge
            var buildRisingEdge = Build && !_lastBuildTrigger;
            _lastBuildTrigger = Build;

            if (buildRisingEdge && !_isBuilding && PLYData != null && PLYData.Count > 0)
                StartBuilding();

            UpdateOutputs();

            if (_isBuilding && _progressInfo != null && _progressInfo.IsCompleted)
            {
                _isBuilding = false;

                if (_progressInfo.Error == null)
                {
                    NodeBufferData = _progressInfo.NodeBufferData ?? Array.Empty<byte>();
                    IndexBufferData = _progressInfo.IndexBufferData ?? Array.Empty<byte>();
                    NodeCount = _progressInfo.NodeCount;
                    IndexCount = _progressInfo.IndexCount;
                    TotalMemoryUsed = _progressInfo.TotalMemoryUsed;
                    NodesProcessed = _progressInfo.NodesProcessed;

                    CalculateStatistics();
                }
            }

            IsBuilding = _isBuilding;
        }
        
        private void Log(string message)
        {
            if (Debug) Console.WriteLine(message);
        }

        private async void StartBuilding()
        {
            _isBuilding = true;
            _wasCacheHit = false;            // NEW: reset per build
            _hasPrecomputedLODs = false;     // reset (will be filled from cache/build)
            _progressInfo = new GPUOctree.OctreeProgressInfo();
            var swTotal = Stopwatch.StartNew();

            // Clear outputs
            NodeBufferData = Array.Empty<byte>();
            IndexBufferData = Array.Empty<byte>();
            NodeCount = 0;
            IndexCount = 0;
            TotalMemoryUsed = 0;
            HasError = false;
            ErrorMessage = "";

            var config = new GPUOctree.BuildConfig
            {
                MaxDepth = MaxDepth,
                MaxPointsPerLeaf = MaxPointsPerLeaf
            };

            try
            {
                if (!string.IsNullOrWhiteSpace(CacheBasePath))
                    DiskCachePaths.BasePath = CacheBasePath;

                if (UseDiskCache)
                {
                    var pcKey = !string.IsNullOrWhiteSpace(PointCloudKey) ? PointCloudKey : DerivePointCloudKey(PLYData);
                    var octreeKey = DiskCacheKey.ForOctree(pcKey, MaxDepth, MaxPointsPerLeaf, 0f, config.OptimizeForSpeed, config.EnableDetailedValidation);
                    if (ForceCacheUpdate)
                        DiskCache.Invalidate("octree", octreeKey);

                    if (DiskCache.TryGet("octree", octreeKey, new OctreeCacheSerializer(), default, out var payloadTask))
                    {
                        var swHit = Stopwatch.StartNew();
                        var payload = await payloadTask;
                        swHit.Stop();
                        Log($"[OctreeBuilder] Cache hit. Read in {swHit.ElapsedMilliseconds} ms. Nodes={payload.NodeCount} Indices={payload.IndexCount}");
                        _wasCacheHit = true;                         // NEW
                        _hasPrecomputedLODs = payload.HasPrecomputedLODs;  // NEW: remember flag from cache

                        _progressInfo.StageName = "Loaded from cache";
                        _progressInfo.ProgressPercentage = 100;
                        _progressInfo.NodeBufferData = payload.NodeBuffer;
                        _progressInfo.IndexBufferData = payload.IndexBuffer;
                        _progressInfo.NodeCount = payload.NodeCount;
                        _progressInfo.IndexCount = payload.IndexCount;
                        _progressInfo.TotalMemoryUsed = payload.TotalMemory;
                        _progressInfo.IsCompleted = true;
                    }
                    else
                    {
                        var swMiss = Stopwatch.StartNew();
                        var payload = await DiskCache.GetOrCreateAsync(
                            cacheNamespace: "octree",
                            key: octreeKey,
                            serializer: new OctreeCacheSerializer(),
                            buildAsync: async ct =>
                            {
                                var swBuild = Stopwatch.StartNew();
                                await GPUOctree.BuildInBackgroundAsync(PLYData, _progressInfo, config);
                                swBuild.Stop();
                                Log($"[OctreeBuilder] Built from source in {swBuild.ElapsedMilliseconds} ms. Nodes={_progressInfo.NodeCount} Indices={_progressInfo.IndexCount}");

                                // Precompute LODs BEFORE writing to cache (only once per dataset)
                                bool precomputed = false;
                                if (EnablePrecomputedLeafLODs && _progressInfo.Error == null)
                                {
                                    StageSafe("Precompute LODs (Leaves, caching)", 0.0);
                                    TryApplyPrecomputedLeafLODs(
                                        _progressInfo.NodeBufferData ?? Array.Empty<byte>(),
                                        _progressInfo.IndexBufferData ?? Array.Empty<byte>(),
                                        _progressInfo.NodeCount,
                                        _progressInfo.IndexCount,
                                        PLYData,
                                        LeafTargetCellsOnLongestAxis);
                                    StageSafe("Precompute LODs (Leaves, caching) – done", 1.0);
                                    precomputed = true;
                                }

                                return new OctreeCacheSerializer.Payload(
                                    _progressInfo.NodeBufferData!,
                                    _progressInfo.IndexBufferData!,
                                    _progressInfo.NodeCount,
                                    _progressInfo.IndexCount,
                                    _progressInfo.TotalMemoryUsed,
                                    precomputed);
                            },
                            ct: default);
                        swMiss.Stop();
                        Log($"[OctreeBuilder] Cache miss. Build+write in {swMiss.ElapsedMilliseconds} ms.");
                        _buildTask = Task.CompletedTask;
                        if (_progressInfo != null && _progressInfo.Error == null)
                        {
                            _progressInfo.NodeBufferData = payload.NodeBuffer;
                            _progressInfo.IndexBufferData = payload.IndexBuffer;
                            _progressInfo.NodeCount = payload.NodeCount;
                            _progressInfo.IndexCount = payload.IndexCount;
                            _progressInfo.TotalMemoryUsed = payload.TotalMemory;
                            _progressInfo.IsCompleted = true;
                            _hasPrecomputedLODs = payload.HasPrecomputedLODs;   // NEW: propagate flag
                        }
                    }
                }
                else
                {
                    // No disk cache: build + optional LODs in this run
                    var swNoCache = Stopwatch.StartNew();
                    _buildTask = GPUOctree.BuildInBackgroundAsync(PLYData, _progressInfo, config);
                    await _buildTask;
                    swNoCache.Stop();
                    Log($"[OctreeBuilder] Built without cache in {swNoCache.ElapsedMilliseconds} ms. Nodes={_progressInfo?.NodeCount} Indices={_progressInfo?.IndexCount}");
                    _hasPrecomputedLODs = false; // will be set true if we run LODs below
                }

                // ---------------------------------------------
                // Post-build LOD precompute:
                // - For disk cache: ONLY in the buildAsync above.
                // - For no-cache: do it here once per run.
                // ---------------------------------------------
                if (_progressInfo != null &&
                    _progressInfo.Error == null &&
                    EnablePrecomputedLeafLODs &&
                    !_hasPrecomputedLODs &&
                    !UseDiskCache)              // NEW: skip on cache hits/misses (cache path handled above)
                {
                    StageSafe("Precompute LODs (Leaves)", 0.0);
                    TryApplyPrecomputedLeafLODs(
                        _progressInfo.NodeBufferData ?? Array.Empty<byte>(),
                        _progressInfo.IndexBufferData ?? Array.Empty<byte>(),
                        _progressInfo.NodeCount,
                        _progressInfo.IndexCount,
                        PLYData,
                        LeafTargetCellsOnLongestAxis);
                    StageSafe("Precompute LODs (Leaves) – done", 1.0);
                    _hasPrecomputedLODs = true;
                }
            }
            catch (Exception ex)
            {
                if (_progressInfo != null)
                {
                    _progressInfo.Error = ex;
                    _progressInfo.IsCompleted = true;
                    _progressInfo.StageName = "Error";
                }
            }
            finally
            {
                swTotal.Stop();
                Log($"[OctreeBuilder] Total elapsed {swTotal.ElapsedMilliseconds} ms.");
            }
        }

        private static string DerivePointCloudKey(Dictionary<string, float[]> ply)
        {
            if (ply == null || ply.Count == 0) return "empty";
            var keys = new List<string>(ply.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var k in keys)
            {
                var len = ply[k]?.LongLength ?? 0;
                total += len;
                sb.Append(k).Append(':').Append(len).Append('|');
            }
            sb.Append("sum:").Append(total);
            return sb.ToString();
        }

        private void UpdateOutputs()
        {
            if (_progressInfo != null)
            {
                Progress = (float)_progressInfo.ProgressPercentage / 100.0f;
                Status = _isBuilding ? "Building" : _progressInfo.IsCompleted ? "Complete" : "Ready";
                StageName = _progressInfo.StageName ?? "";
                TotalPoints = _progressInfo.TotalPoints;
                IsCompleted = _progressInfo.IsCompleted;
                HasError = _progressInfo.Error != null;
                ErrorMessage = _progressInfo.Error?.Message ?? "";
            }
        }

        private void CalculateStatistics()
        {
            if (_progressInfo != null && TotalPoints > 0)
            {
                CompressionRatio = (float)IndexCount / TotalPoints;
                NodeBufferSizeMB = (NodeBufferData?.Length ?? 0) / (1024f * 1024f);
                IndexBufferSizeMB = (IndexBufferData?.Length ?? 0) / (1024f * 1024f);
                TotalSizeMB = NodeBufferSizeMB + IndexBufferSizeMB;
            }
        }

        private void StageSafe(string name, double? progressOverride = null)
        {
            try
            {
                if (_progressInfo != null)
                {
                    _progressInfo.StageName = name;
                    if (progressOverride.HasValue)
                        _progressInfo.ProgressPercentage = Math.Max(_progressInfo.ProgressPercentage, progressOverride.Value * 100.0);
                }
            }
            catch { /* no-op */ }
        }

        // ===========================
        // Precompute-LODs (Leaves)
        // ===========================

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct CpuNode
        {
            public Vector3 BoundsMin;
            public float BoundsSize;
            public Vector3 BoundsMax;
            public uint ChildStartIndex;
            public uint IndexStartIndex;
            public uint IndexCount;
            public uint LODLevel;
            public uint ParentIndex;

            public bool IsLeaf =>
                ChildStartIndex == 0xFFFFFFFFu || unchecked((int)ChildStartIndex) == -1;

            public Bounds ToBounds()
                => new Bounds { Min = BoundsMin, Max = BoundsMax };
        }

        private struct Bounds
        {
            public Vector3 Min, Max;
            public Vector3 Size => Max - Min;
        }

        private void TryApplyPrecomputedLeafLODs(
            byte[] nodeBufferData,
            byte[] indexBufferData,
            int nodeCount,
            int indexCount,
            Dictionary<string, float[]> plyData,
            int leafTargetCellsOnLongestAxis)
        {
            if (nodeBufferData == null || indexBufferData == null || nodeCount <= 0 || indexCount <= 0)
                return;

            if (!TryGetPositions(plyData, out var posX, out var posY, out var posZ) ||
                posX.Length != posY.Length || posX.Length != posZ.Length)
                return;

            var nodeBytes   = nodeBufferData;
            var indexBytes  = indexBufferData;

            var posVec = new Vector3[posX.Length];
            for (int i = 0; i < posVec.Length; i++)
                posVec[i] = new Vector3(posX[i], posY[i], posZ[i]);

            int targetCells = Math.Max(4, leafTargetCellsOnLongestAxis);

            System.Threading.Tasks.Parallel.For(0, nodeCount, i =>
            {
                // Spans LOKAL erzeugen (keine Captures von Span)
                var nodeSpan    = MemoryMarshal.Cast<byte, CpuNode>(nodeBytes.AsSpan());
                var indicesSpan = MemoryMarshal.Cast<byte, int>(indexBytes.AsSpan());

                var n = nodeSpan[i];
                bool isLeaf = (n.ChildStartIndex == 0xFFFFFFFFu) || unchecked((int)n.ChildStartIndex) == -1;
                if (!isLeaf) return;

                int start = (int)n.IndexStartIndex;
                int count = (int)n.IndexCount;
                if ((uint)start >= (uint)indicesSpan.Length || count <= 1 || (start + count) > indicesSpan.Length)
                    return;

                var b = new Bounds { Min = n.BoundsMin, Max = n.BoundsMax };
                var size = b.Size;
                float longest = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
                float baseCell = longest > 0 ? longest / targetCells : 1e-4f;

                var slice = indicesSpan.Slice(start, count);
                ProgressiveOrderLeaf(slice, posVec, in b, baseCell);
            });

            // --- DEBUG ---
            int nonEmpty = 0, empty = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                var n = MemoryMarshal.Cast<byte, CpuNode>(nodeBufferData.AsSpan())[i];
                if (n.IndexCount > 0) nonEmpty++; else empty++;
            }

            Console.WriteLine($"[LOD Permutation] Nodes: {nodeCount}, nonEmpty={nonEmpty}, totalIndices={indexCount}");

            var allIndicesSpan = MemoryMarshal.Cast<byte, int>(indexBufferData.AsSpan());
            int previewCount = Math.Min(16, indexCount);
            if (previewCount > 0)
            {
                var preview = allIndicesSpan.Slice(0, previewCount).ToArray();
                Console.WriteLine($"First few indices: {string.Join(",", preview)}");
            }
        }

        private static bool TryGetPositions(
            Dictionary<string, float[]> ply,
            out float[] x, out float[] y, out float[] z)
        {
            x = y = z = Array.Empty<float>();
            if (ply == null) return false;

            bool ok =
                TryGetCaseInsensitive(ply, "x", out x) &&
                TryGetCaseInsensitive(ply, "y", out y) &&
                TryGetCaseInsensitive(ply, "z", out z);
            return ok;
        }

        private static bool TryGetCaseInsensitive(Dictionary<string, float[]> dict, string key, out float[] arr)
        {
            foreach (var kv in dict)
            {
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    arr = kv.Value;
                    return true;
                }
            }
            arr = Array.Empty<float>();
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Hash32(int x)
        {
            unchecked
            {
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                return x < 0 ? -x : x;
            }
        }

        private static void ShuffleDeterministic(Span<int> span, int seed)
        {
            int n = span.Length;
            int s = Hash32(seed);
            for (int i = n - 1; i > 0; --i)
            {
                s = unchecked(s * 1664525 + 1013904223);
                int j = (int)((uint)s % (uint)(i + 1));
                (span[i], span[j]) = (span[j], span[i]);
            }
        }

        private readonly struct BoundsKeySeed
        {
            public static int FromBounds(in Bounds b)
            {
                int hx = BitConverter.SingleToInt32Bits(b.Min.X);
                int hy = BitConverter.SingleToInt32Bits(b.Min.Y);
                int hz = BitConverter.SingleToInt32Bits(b.Min.Z);
                int HX = BitConverter.SingleToInt32Bits(b.Max.X);
                int HY = BitConverter.SingleToInt32Bits(b.Max.Y);
                int HZ = BitConverter.SingleToInt32Bits(b.Max.Z);
                return Hash32(hx ^ hy ^ hz ^ HX ^ HY ^ HZ);
            }
        }

        private static void ProgressiveOrderLeaf(
            Span<int> indices,
            ReadOnlySpan<Vector3> positions,
            in Bounds b,
            float baseCellSize)
        {
            if (indices.Length <= 1) return;

            var used = ArrayPool<byte>.Shared.Rent(indices.Length);
            try
            {
                Array.Clear(used, 0, indices.Length);

                ShuffleDeterministic(indices, BoundsKeySeed.FromBounds(in b));

                var size = b.Size;
                float longest = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
                float baseCell = MathF.Max(1e-4f, baseCellSize);
                int levels = Math.Max(1, (int)MathF.Ceiling(MathF.Log2(MathF.Max(1e-6f, longest / baseCell))));

                int w = 0;

                static long CellKey(int x, int y, int z) =>
                    ((long)(uint)(x & 0x1FFFFF) << 42) | ((long)(uint)(y & 0x1FFFFF) << 21) | (uint)(z & 0x1FFFFF);

                for (int level = 0; level < levels; level++)
                {
                    float cell = baseCell * (1 << level);
                    var seen = new Dictionary<long, byte>(Math.Min(indices.Length, 4096));

                    for (int i = 0; i < indices.Length; i++)
                    {
                        if (used[i] != 0) continue;

                        int id = indices[i];
                        var p = positions[id];

                        int gx = (int)MathF.Floor((p.X - b.Min.X) / cell);
                        int gy = (int)MathF.Floor((p.Y - b.Min.Y) / cell);
                        int gz = (int)MathF.Floor((p.Z - b.Min.Z) / cell);
                        long key = CellKey(gx, gy, gz);

                        if (!seen.ContainsKey(key))
                        {
                            seen[key] = 1;
                            used[i] = 1;
                            indices[w++] = id;
                            if (w == indices.Length) break;
                        }
                    }

                    if (w == indices.Length) break;
                }

                for (int i = 0; i < indices.Length; i++)
                    if (used[i] == 0)
                        indices[w++] = indices[i];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(used);
            }
        }
    }
}
