using System.Diagnostics;
using System.Text;

namespace Fuse.IO.Ply;
#pragma warning disable CS1591
// VVVV Process Node für GPU-Octree-Build + CPU-seitiges Precompute-LOD (Leaves)
[ProcessNode]
public class OctreeBuilder : ProcessNodeBase
{
    private Task? _buildTask;
    private bool _hasPrecomputedLODs;
    private bool _isBuilding;
    private bool _lastBuildTrigger;
    private GPUOctree.OctreeProgressInfo? _progressInfo;
    private CancellationTokenSource? _buildCts;
    private bool _allocationLoggedForRun;

    public OctreeBuilder() : base("OctreeBuilder")
    {
        _buildCts = new CancellationTokenSource();
    }
    public Dictionary<string, float[]> PLYData { private get; set; } = new(0);
    public bool Build { private get; set; }
    public int MaxDepth { private get; set; } = 8;
    public int MaxPointsPerLeaf { private get; set; } = 5000;
    public bool UseDiskCache { private get; set; }
    public bool ForceCacheUpdate { private get; set; }
    public string CacheBasePath { private get; set; } = string.Empty;
    public string PointCloudKey { private get; set; } = string.Empty;
    public bool Debug { private get; set; }

    /// <summary>
    ///     Aktiviert die CPU-seitige Progressive-LOD-Permutation für Leaf-Nodes.
    /// </summary>
    public bool EnablePrecomputedLeafLODs { private get; set; } = true;

    /// <summary>
    ///     Grobe Zielanzahl Zellen auf längster Kante pro Leaf (steuert die Grob→Fein-Layer).
    ///     Größer = mehr Grobstruktur zuerst. 24–48 sind gute Startwerte.
    /// </summary>
    public int LeafTargetCellsOnLongestAxis { private get; set; } = 32;

    public float Progress { get; private set; }
    public string Status { get; private set; } = "Ready";
    public string StageName { get; private set; } = "";
    public int TotalPoints { get; private set; }
    public bool IsBuilding { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool HasError { get; private set; }
    public string ErrorMessage { get; private set; } = "";
    public byte[] NodeBufferData { get; private set; } = Array.Empty<byte>();
    public byte[] IndexBufferData { get; private set; } = Array.Empty<byte>();
    public int NodeCount { get; private set; }
    public int IndexCount { get; private set; }
    public int NodesProcessed { get; private set; }
    public long TotalMemoryUsed { get; private set; }
    public float CompressionRatio { get; private set; }
    public float NodeBufferSizeMB { get; private set; }
    public float IndexBufferSizeMB { get; private set; }
    public float TotalSizeMB { get; private set; }

    public void Update()
    {
        UpdateDiagnostics(Debug);
        if (IsDisposed) return;

        // Rising edge
        var buildRisingEdge = IsRisingEdge(ref _lastBuildTrigger, Build);

        if (buildRisingEdge && !_isBuilding)
        {
            if (PLYData == null || PLYData.Count == 0)
            {
                Console.WriteLine($"[OctreeBuilder] WARNING: Build triggered but PLYData is empty! PLYData={PLYData?.Count ?? 0} arrays. Check FastPly load - you may need ForceReload=true on the FastPly node.");
            }
            else
            {
                var firstFieldLength = PLYData.Values.FirstOrDefault()?.Length ?? 0;
                Console.WriteLine($"[OctreeBuilder] Build triggered with PLYData: {PLYData.Count} arrays, {firstFieldLength:N0} vertices");
                StartBuilding();
            }
        }

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
                if (!_allocationLoggedForRun)
                {
                    _allocationLoggedForRun = true;
                    TrackAllocationEstimate("build-complete", EstimateRetainedBytes());
                }
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
        var run = BeginRun(ref _buildCts);
        var generation = run.generation;
        var ct = run.token;
        var octreeInputData = BuildPositionOnlyView(PLYData);
        _isBuilding = true;
        _hasPrecomputedLODs = false; // reset (will be filled from cache/build)
        _allocationLoggedForRun = false;
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
                Log($"[OctreeBuilder] PointCloudKey derived: {(pcKey.Length > 100 ? pcKey.Substring(0, 100) + "..." : pcKey)}");
                
                if (pcKey == "empty")
                {
                    Console.WriteLine("[OctreeBuilder] WARNING: PointCloudKey is 'empty' - PLYData has no arrays! Cache will use wrong key.");
                }
                
                var octreeKey = DiskCacheKey.ForOctree(pcKey, MaxDepth, MaxPointsPerLeaf, 0f, config.OptimizeForSpeed,
                    config.EnableDetailedValidation);
                if (ForceCacheUpdate)
                {
                    Log("[OctreeBuilder] ForceCacheUpdate=true, invalidating octree cache");
                    DiskCache.Invalidate("octree", octreeKey);
                }

                async Task<OctreeCacheSerializer.Payload> BuildOctreePayloadAsync(CancellationToken ct)
                {
                    if (!IsCurrentGeneration(generation))
                        throw new ObjectDisposedException(nameof(OctreeBuilder));

                    var swBuild = Stopwatch.StartNew();
                    await GPUOctree.BuildInBackgroundAsync(octreeInputData, _progressInfo, config, ct);
                    swBuild.Stop();
                    Log(
                        $"[OctreeBuilder] Built from source in {swBuild.ElapsedMilliseconds} ms. Nodes={_progressInfo.NodeCount} Indices={_progressInfo.IndexCount}");

                    if (_progressInfo.Error != null)
                        throw _progressInfo.Error;
                    if (_progressInfo.NodeBufferData == null || _progressInfo.IndexBufferData == null)
                        throw new InvalidOperationException("Octree build completed without output buffers.");

                    // Precompute LODs BEFORE writing to cache (only once per dataset)
                    var precomputed = false;
                    if (EnablePrecomputedLeafLODs)
                    {
                        StageSafe("Precompute LODs (Leaves, caching)", 0.0);
                        precomputed = OctreeLodHelper.TryApplyPrecomputedLeafLODs(
                            _progressInfo.NodeBufferData,
                            _progressInfo.IndexBufferData,
                            _progressInfo.NodeCount,
                            _progressInfo.IndexCount,
                            octreeInputData,
                            LeafTargetCellsOnLongestAxis,
                            Debug);
                        StageSafe("Precompute LODs (Leaves, caching) - done", 1.0);
                    }

                    return new OctreeCacheSerializer.Payload(
                        _progressInfo.NodeBufferData,
                        _progressInfo.IndexBufferData,
                        _progressInfo.NodeCount,
                        _progressInfo.IndexCount,
                        _progressInfo.TotalMemoryUsed,
                        precomputed);
                }

                if (DiskCache.TryGet("octree", octreeKey, new OctreeCacheSerializer(), default, out var payloadTask))
                {
                    var swHit = Stopwatch.StartNew();
                    var payload = await payloadTask;
                    swHit.Stop();
                    Log(
                        $"[OctreeBuilder] Cache hit. Read in {swHit.ElapsedMilliseconds} ms. Nodes={payload.NodeCount} Indices={payload.IndexCount}");
                    if (!IsValidOctreePayload(payload))
                    {
                        Console.WriteLine(
                            "[OctreeBuilder] WARNING: Octree cache payload invalid. Invalidating and rebuilding.");
                        DiskCache.Invalidate("octree", octreeKey);
                        payload = await DiskCache.GetOrCreateAsync(
                            "octree",
                            octreeKey,
                            new OctreeCacheSerializer(),
                            BuildOctreePayloadAsync,
                            default);
                    }
                    _hasPrecomputedLODs = payload.HasPrecomputedLODs; // NEW: remember flag from cache

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
                        "octree",
                        octreeKey,
                        new OctreeCacheSerializer(),
                        BuildOctreePayloadAsync,
                        default);
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
                        _hasPrecomputedLODs = payload.HasPrecomputedLODs; // NEW: propagate flag
                    }
                }
            }
            else
            {
                // No disk cache: build + optional LODs in this run
                var swNoCache = Stopwatch.StartNew();
                _buildTask = GPUOctree.BuildInBackgroundAsync(octreeInputData, _progressInfo, config, ct);
                await _buildTask;
                swNoCache.Stop();
                Log(
                    $"[OctreeBuilder] Built without cache in {swNoCache.ElapsedMilliseconds} ms. Nodes={_progressInfo?.NodeCount} Indices={_progressInfo?.IndexCount}");
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
                !UseDiskCache) // NEW: skip on cache hits/misses (cache path handled above)
            {
                if (!IsCurrentGeneration(generation)) return;
                StageSafe("Precompute LODs (Leaves)", 0.0);
                var lodSuccess = OctreeLodHelper.TryApplyPrecomputedLeafLODs(
                    _progressInfo.NodeBufferData ?? Array.Empty<byte>(),
                    _progressInfo.IndexBufferData ?? Array.Empty<byte>(),
                    _progressInfo.NodeCount,
                    _progressInfo.IndexCount,
                    octreeInputData,
                    LeafTargetCellsOnLongestAxis,
                    Debug);
                StageSafe("Precompute LODs (Leaves) - done", 1.0);
                _hasPrecomputedLODs = lodSuccess;
            }
        }
        catch (Exception ex)
        {
            if (!IsCurrentGeneration(generation)) return;
            if (ex is OperationCanceledException)
            {
                _isBuilding = false;
                return;
            }
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
        var sb = new StringBuilder();
        foreach (var k in keys)
        {
            var len = ply[k]?.LongLength ?? 0;
            total += len;
            sb.Append(k).Append(':').Append(len).Append('|');
        }

        sb.Append("sum:").Append(total);
        return sb.ToString();
    }

    private static Dictionary<string, float[]> BuildPositionOnlyView(Dictionary<string, float[]> source)
    {
        var result = new Dictionary<string, float[]>(3, StringComparer.OrdinalIgnoreCase);
        if (source == null || source.Count == 0)
            return result;

        foreach (var kv in source)
        {
            if (string.Equals(kv.Key, "x", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kv.Key, "y", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kv.Key, "z", StringComparison.OrdinalIgnoreCase))
            {
                result[kv.Key] = kv.Value;
            }
        }

        return result;
    }

    private static bool IsValidOctreePayload(OctreeCacheSerializer.Payload payload)
    {
        if (payload.NodeCount < 0 || payload.IndexCount < 0)
            return false;
        if (payload.NodeCount > 0 && (payload.NodeBuffer == null || payload.NodeBuffer.Length == 0))
            return false;
        if (payload.IndexCount > 0 && (payload.IndexBuffer == null || payload.IndexBuffer.Length == 0))
            return false;
        return true;
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
                    _progressInfo.ProgressPercentage =
                        Math.Max(_progressInfo.ProgressPercentage, progressOverride.Value * 100.0);
            }
        }
        catch
        {
            /* no-op */
        }
    }

    private void TryApplyPrecomputedLeafLODs(
        byte[] nodeBufferData,
        byte[] indexBufferData,
        int nodeCount,
        int indexCount,
        Dictionary<string, float[]> plyData,
        int leafTargetCellsOnLongestAxis)
    {
        // Delegate to shared helper
        OctreeLodHelper.TryApplyPrecomputedLeafLODs(
            nodeBufferData,
            indexBufferData,
            nodeCount,
            indexCount,
            plyData,
            leafTargetCellsOnLongestAxis,
            Debug);
    }

    private void ClearAllRetainedData(string reason)
    {
        TrackReleaseEstimate(reason, EstimateRetainedBytes, () =>
        {
            NodeBufferData = Array.Empty<byte>();
            IndexBufferData = Array.Empty<byte>();
            if (_progressInfo != null)
            {
                _progressInfo.NodeBufferData = Array.Empty<byte>();
                _progressInfo.IndexBufferData = Array.Empty<byte>();
            }

            NodeCount = 0;
            IndexCount = 0;
            NodesProcessed = 0;
            TotalMemoryUsed = 0;
            CompressionRatio = 0;
            NodeBufferSizeMB = 0;
            IndexBufferSizeMB = 0;
            TotalSizeMB = 0;
        });
    }

    protected override void OnDisposeManaged()
    {
        _isBuilding = false;
        IsBuilding = false;
        CancelAndDisposeCts(ref _buildCts);
        _buildTask = null;
        _progressInfo = null;
        ClearAllRetainedData("Dispose");
        Status = "Disposed";
        StageName = string.Empty;
        IsCompleted = false;
        HasError = false;
        ErrorMessage = string.Empty;
        Progress = 0;
        TotalPoints = 0;
    }

    private long EstimateRetainedBytes()
    {
        long bytes = 0;
        if (NodeBufferData != null)
            bytes += NodeBufferData.LongLength;
        if (IndexBufferData != null)
            bytes += IndexBufferData.LongLength;
        return bytes;
    }

}


