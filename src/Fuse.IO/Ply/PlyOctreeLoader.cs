using System.Diagnostics;

namespace Fuse.IO.Ply;

#pragma warning disable CS1591

/// <summary>
/// Combined ProcessNode that loads PLY files and builds GPU octrees in a single operation.
/// Provides unified progress reporting across both stages (0-100% overall).
///
/// This node combines FastPly + OctreeBuilder for convenience when you need both
/// the point cloud data and the octree for GPU rendering. It offers:
/// - Single trigger to load and build
/// - Unified progress (0-100%) across all stages
/// - Shared disk caching for the combined result
/// - Better error handling across stages
///
/// Use this instead of chaining FastPly + OctreeBuilder when you need both outputs.
/// </summary>
[ProcessNode]
public class PlyOctreeLoader
{
    // State tracking
    private bool _isProcessing;
    private bool _lastLoadTrigger;
    private CombinedProgressInfo? _progressInfo;

    // Internal progress trackers (for delegation to core methods)
    private FastPlyReader.ProgressInfo? _plyProgressInfo;
    private GPUOctree.OctreeProgressInfo? _octreeProgressInfo;
    private bool _hasPrecomputedLODs;

    // Diagnostic logging
    private readonly int _instanceId;
    private int _updateCount;
    private int _triggerCount;

    public PlyOctreeLoader()
    {
        _instanceId = PlyDiagnosticLog.GetInstanceId(this);
    }

    #region Inputs

    // File input
    public string FilePath { private get; set; } = string.Empty;
    public bool Load { private get; set; }

    // PLY options
    public PlyDecimationStrategy DecimationStrategy { private get; set; } = PlyDecimationStrategy.None;
    public int DecimationFactor { private get; set; } = 1;

    // Octree options
    public int MaxDepth { private get; set; } = 8;
    public int MaxPointsPerLeaf { private get; set; } = 5000;

    /// <summary>
    /// Enables CPU-side progressive LOD permutation for leaf nodes.
    /// This reorders points within leaves for coarse-to-fine progressive rendering.
    /// </summary>
    public bool EnablePrecomputedLeafLODs { private get; set; } = true;

    /// <summary>
    /// Target cell count on longest axis per leaf (controls coarse→fine layers).
    /// Larger = more coarse structure first. 24-48 are good starting values.
    /// </summary>
    public int LeafTargetCellsOnLongestAxis { private get; set; } = 32;

    // Cache options
    public bool UseDiskCache { private get; set; }
    public bool ForceReload { private get; set; }
    public string CacheBasePath { private get; set; } = string.Empty;
    public bool Debug { private get; set; }

    #endregion

    #region Outputs - PLY Data

    /// <summary>
    /// Dictionary of field name to float array. Each field is stored separately (SoA layout).
    /// For example: PlyData["x"] contains all X coordinates, PlyData["red"] contains all red values.
    /// </summary>
    public Dictionary<string, float[]> PlyData { get; private set; } = new(0);

    /// <summary>
    /// Number of vertices in the loaded point cloud.
    /// </summary>
    public int VertexCount { get; private set; }

    /// <summary>
    /// Names of fields in order as they appear in the PLY header.
    /// For example: ["x", "y", "z", "red", "green", "blue"]
    /// </summary>
    public string[] FieldOrder { get; private set; } = Array.Empty<string>();

    #endregion

    #region Outputs - Octree Data

    /// <summary>
    /// GPU-ready octree node buffer (array of GPUOctreeNode structs as bytes).
    /// </summary>
    public byte[] NodeBufferData { get; private set; } = Array.Empty<byte>();

    /// <summary>
    /// GPU-ready point index buffer (array of int indices as bytes).
    /// </summary>
    public byte[] IndexBufferData { get; private set; } = Array.Empty<byte>();

    /// <summary>
    /// Number of octree nodes.
    /// </summary>
    public int NodeCount { get; private set; }

    /// <summary>
    /// Number of point indices.
    /// </summary>
    public int IndexCount { get; private set; }

    #endregion

    #region Outputs - Progress/Status

    /// <summary>
    /// Overall progress from 0 to 1 (not 0-100).
    /// </summary>
    public float Progress { get; private set; }

    /// <summary>
    /// Current processing status: "Ready", "Processing", "Complete", or "Error".
    /// </summary>
    public string Status { get; private set; } = "Ready";

    /// <summary>
    /// Current stage name: "Loading PLY", "Building Octree", "Generating LODs", etc.
    /// </summary>
    public string StageName { get; private set; } = string.Empty;

    /// <summary>
    /// Detailed status within the current stage.
    /// </summary>
    public string DetailedStatus { get; private set; } = string.Empty;

    /// <summary>
    /// True while loading/building is in progress.
    /// </summary>
    public bool IsProcessing { get; private set; }

    /// <summary>
    /// True when the entire operation has completed (success or error).
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// True if an error occurred during processing.
    /// </summary>
    public bool HasError { get; private set; }

    /// <summary>
    /// Error message if HasError is true.
    /// </summary>
    public string ErrorMessage { get; private set; } = string.Empty;

    #endregion

    #region Outputs - Statistics

    public int TotalPoints { get; private set; }
    public float NodeBufferSizeMB { get; private set; }
    public float IndexBufferSizeMB { get; private set; }
    public float TotalSizeMB { get; private set; }
    public long TotalMemoryUsed { get; private set; }

    #endregion

    /// <summary>
    /// Returns this instance for chaining or downstream reference.
    /// </summary>
    public PlyOctreeLoader Output => this;

    public void Update()
    {
        _updateCount++;

        // Rising edge detection for Load trigger
        var loadRisingEdge = Load && !_lastLoadTrigger;

        if (Debug && Load != _lastLoadTrigger)
        {
            PlyDiagnosticLog.Write("PlyOctreeLoader", _instanceId,
                $"Load edge: _lastLoadTrigger={_lastLoadTrigger} -> Load={Load}, risingEdge={loadRisingEdge}, _isProcessing={_isProcessing}, IsCompleted={IsCompleted}, file={(string.IsNullOrEmpty(FilePath) ? "<empty>" : Path.GetFileName(FilePath))}, frame={_updateCount}");
        }

        _lastLoadTrigger = Load;

        if (loadRisingEdge && !_isProcessing && !string.IsNullOrEmpty(FilePath))
        {
            _triggerCount++;
            if (Debug)
                PlyDiagnosticLog.Write("PlyOctreeLoader", _instanceId,
                    $"TRIGGER #{_triggerCount} - risingEdge detected, starting processing. file={Path.GetFileName(FilePath)}, frame={_updateCount}");
            StartProcessing();
        }

        UpdateOutputs();
    }

    private async void StartProcessing()
    {
        _isProcessing = true;
        IsCompleted = false;
        HasError = false;
        ErrorMessage = string.Empty;
        _hasPrecomputedLODs = false;

        _progressInfo = new CombinedProgressInfo { StartTime = DateTime.UtcNow };
        _plyProgressInfo = new FastPlyReader.ProgressInfo();
        _octreeProgressInfo = new GPUOctree.OctreeProgressInfo();

        // Clear outputs
        PlyData = new Dictionary<string, float[]>(0);
        VertexCount = 0;
        FieldOrder = Array.Empty<string>();
        NodeBufferData = Array.Empty<byte>();
        IndexBufferData = Array.Empty<byte>();
        NodeCount = 0;
        IndexCount = 0;
        TotalMemoryUsed = 0;

        // Capture inputs to avoid threading issues
        var currentPath = FilePath;
        var currentStrategy = DecimationStrategy;
        var currentFactor = DecimationFactor;
        var currentMaxDepth = MaxDepth;
        var currentMaxPointsPerLeaf = MaxPointsPerLeaf;
        var currentEnableLODs = EnablePrecomputedLeafLODs;
        var currentLeafTargetCells = LeafTargetCellsOnLongestAxis;

        var swTotal = Stopwatch.StartNew();

        try
        {
            if (!string.IsNullOrWhiteSpace(CacheBasePath))
                DiskCachePaths.BasePath = CacheBasePath;

            // ===== STAGE 1: Load PLY =====
            _progressInfo.AdvanceStage(CombinedProgressInfo.ProcessStage.LoadingPly, "Loading PLY");
            Log($"[PlyOctreeLoader] Starting PLY load: {currentPath}");
            Log($"[PlyOctreeLoader] DecimationStrategy={currentStrategy}, DecimationFactor={currentFactor}");

            var loadResult = await PlyLoadCore.LoadSoAAsync(
                currentPath,
                _plyProgressInfo,
                currentStrategy,
                currentFactor,
                UseDiskCache,
                ForceReload,
                CacheBasePath,
                Debug);

            if (_plyProgressInfo.Error != null)
                throw _plyProgressInfo.Error;

            PlyData = loadResult.Arrays;
            VertexCount = loadResult.VertexCount;
            FieldOrder = loadResult.FieldOrder;
            TotalPoints = loadResult.VertexCount;

            Log($"[PlyOctreeLoader] PLY load complete: {VertexCount:N0} vertices, {FieldOrder.Length} fields, CacheHit={loadResult.WasCacheHit}");

            // ===== STAGE 2: Build Octree =====
            _progressInfo.AdvanceStage(CombinedProgressInfo.ProcessStage.BuildingOctree, "Building Octree");
            Log($"[PlyOctreeLoader] Starting octree build: MaxDepth={currentMaxDepth}, MaxPointsPerLeaf={currentMaxPointsPerLeaf}");

            var config = new GPUOctree.BuildConfig
            {
                MaxDepth = currentMaxDepth,
                MaxPointsPerLeaf = currentMaxPointsPerLeaf
            };

            var octreeCacheHit = false;
            if (UseDiskCache)
            {
                // Use file path as base for octree cache key (more stable than PLYData hash)
                var plyKey = DiskCacheKey.ForPly(currentPath, currentStrategy, currentFactor);
                var octreeKey = DiskCacheKey.ForOctree(plyKey, currentMaxDepth, currentMaxPointsPerLeaf, 0f, config.OptimizeForSpeed, config.EnableDetailedValidation);

                if (ForceReload)
                {
                    Log("[PlyOctreeLoader] ForceReload=true, invalidating octree cache");
                    DiskCache.Invalidate("octree", octreeKey);
                }

                async Task<OctreeCacheSerializer.Payload> BuildOctreePayloadAsync(CancellationToken ct)
                {
                    await GPUOctree.BuildInBackgroundAsync(PlyData, _octreeProgressInfo, config);
                    if (_octreeProgressInfo.Error != null)
                        throw _octreeProgressInfo.Error;
                    if (_octreeProgressInfo.NodeBufferData == null || _octreeProgressInfo.IndexBufferData == null)
                        throw new InvalidOperationException("Octree build completed without output buffers.");

                    // Precompute LODs BEFORE writing to cache
                    var precomputed = false;
                    if (currentEnableLODs)
                    {
                        _progressInfo.AdvanceStage(CombinedProgressInfo.ProcessStage.GeneratingLODs, "Generating LODs (caching)");
                        precomputed = OctreeLodHelper.TryApplyPrecomputedLeafLODs(
                            _octreeProgressInfo.NodeBufferData,
                            _octreeProgressInfo.IndexBufferData,
                            _octreeProgressInfo.NodeCount,
                            _octreeProgressInfo.IndexCount,
                            PlyData,
                            currentLeafTargetCells,
                            Debug);
                    }

                    return new OctreeCacheSerializer.Payload(
                        _octreeProgressInfo.NodeBufferData,
                        _octreeProgressInfo.IndexBufferData,
                        _octreeProgressInfo.NodeCount,
                        _octreeProgressInfo.IndexCount,
                        _octreeProgressInfo.TotalMemoryUsed,
                        precomputed);
                }

                if (DiskCache.TryGet("octree", octreeKey, new OctreeCacheSerializer(), default, out var payloadTask))
                {
                    var payload = await payloadTask;
                    Log($"[PlyOctreeLoader] Octree cache hit. Nodes={payload.NodeCount} Indices={payload.IndexCount}");

                    if (!IsValidOctreePayload(payload))
                    {
                        Console.WriteLine(
                            "[PlyOctreeLoader] WARNING: Octree cache payload invalid. Invalidating and rebuilding.");
                        DiskCache.Invalidate("octree", octreeKey);
                        payload = await DiskCache.GetOrCreateAsync(
                            "octree",
                            octreeKey,
                            new OctreeCacheSerializer(),
                            BuildOctreePayloadAsync,
                            default);
                        octreeCacheHit = false;
                    }
                    else
                    {
                        octreeCacheHit = true;
                    }

                    _hasPrecomputedLODs = payload.HasPrecomputedLODs;

                    _octreeProgressInfo.StageName = "Loaded from cache";
                    _octreeProgressInfo.ProgressPercentage = 100;
                    _octreeProgressInfo.NodeBufferData = payload.NodeBuffer;
                    _octreeProgressInfo.IndexBufferData = payload.IndexBuffer;
                    _octreeProgressInfo.NodeCount = payload.NodeCount;
                    _octreeProgressInfo.IndexCount = payload.IndexCount;
                    _octreeProgressInfo.TotalMemoryUsed = payload.TotalMemory;
                    _octreeProgressInfo.IsCompleted = true;
                }
                else
                {
                    Log("[PlyOctreeLoader] Octree cache miss, building...");
                    var payload = await DiskCache.GetOrCreateAsync(
                        "octree",
                        octreeKey,
                        new OctreeCacheSerializer(),
                        BuildOctreePayloadAsync,
                        default);

                    _octreeProgressInfo.NodeBufferData = payload.NodeBuffer;
                    _octreeProgressInfo.IndexBufferData = payload.IndexBuffer;
                    _octreeProgressInfo.NodeCount = payload.NodeCount;
                    _octreeProgressInfo.IndexCount = payload.IndexCount;
                    _octreeProgressInfo.TotalMemoryUsed = payload.TotalMemory;
                    _octreeProgressInfo.IsCompleted = true;
                    _hasPrecomputedLODs = payload.HasPrecomputedLODs;
                }
            }
            else
            {
                // No disk cache: build directly
                await GPUOctree.BuildInBackgroundAsync(PlyData, _octreeProgressInfo, config);
            }

            if (_octreeProgressInfo.Error != null)
                throw _octreeProgressInfo.Error;

            NodeBufferData = _octreeProgressInfo.NodeBufferData ?? Array.Empty<byte>();
            IndexBufferData = _octreeProgressInfo.IndexBufferData ?? Array.Empty<byte>();
            NodeCount = _octreeProgressInfo.NodeCount;
            IndexCount = _octreeProgressInfo.IndexCount;
            TotalMemoryUsed = _octreeProgressInfo.TotalMemoryUsed;

            Log($"[PlyOctreeLoader] Octree build complete: {NodeCount:N0} nodes, {IndexCount:N0} indices, CacheHit={octreeCacheHit}");

            // ===== STAGE 3: Generate LODs =====
            // Skip if already done during cache creation or loaded from cache with LODs
            if (currentEnableLODs && _octreeProgressInfo.Error == null && !_hasPrecomputedLODs)
            {
                _progressInfo.AdvanceStage(CombinedProgressInfo.ProcessStage.GeneratingLODs, "Generating LODs");
                Log("[PlyOctreeLoader] Generating precomputed leaf LODs");

                var lodSuccess = OctreeLodHelper.TryApplyPrecomputedLeafLODs(
                    NodeBufferData,
                    IndexBufferData,
                    NodeCount,
                    IndexCount,
                    PlyData,
                    currentLeafTargetCells,
                    Debug);

                _hasPrecomputedLODs = lodSuccess;
                Log($"[PlyOctreeLoader] LOD generation {(lodSuccess ? "complete" : "skipped")}");
            }

            // ===== COMPLETE =====
            CalculateStatistics();
            _progressInfo.MarkComplete();
            IsCompleted = true;

            swTotal.Stop();
            Log($"[PlyOctreeLoader] Processing complete. Total: {TotalSizeMB:F1} MB, Elapsed: {swTotal.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            swTotal.Stop();
            Log($"[PlyOctreeLoader] Error after {swTotal.ElapsedMilliseconds} ms: {ex.Message}");
            _progressInfo!.MarkError(ex);
            HasError = true;
            ErrorMessage = ex.Message;
            IsCompleted = true;
        }
        finally
        {
            _isProcessing = false;
            if (Debug)
                PlyDiagnosticLog.Write("PlyOctreeLoader", _instanceId,
                    $"Processing finished. _isProcessing=false, IsCompleted={IsCompleted}, HasError={HasError}, Load={Load}, _lastLoadTrigger={_lastLoadTrigger}");
        }
    }

    private void UpdateOutputs()
    {
        if (_progressInfo == null) return;

        // Update from internal progress trackers based on current stage
        switch (_progressInfo.CurrentStage)
        {
            case CombinedProgressInfo.ProcessStage.LoadingPly:
                if (_plyProgressInfo != null)
                    _progressInfo.UpdateFromPlyProgress(_plyProgressInfo);
                break;

            case CombinedProgressInfo.ProcessStage.BuildingOctree:
                if (_octreeProgressInfo != null)
                    _progressInfo.UpdateFromOctreeProgress(_octreeProgressInfo, false);
                break;

            case CombinedProgressInfo.ProcessStage.GeneratingLODs:
                if (_octreeProgressInfo != null)
                    _progressInfo.UpdateFromOctreeProgress(_octreeProgressInfo, true);
                break;
        }

        Progress = (float)_progressInfo.OverallProgress / 100f;
        StageName = _progressInfo.StageName;
        DetailedStatus = _progressInfo.DetailedStatus;
        Status = _isProcessing ? "Processing" : (_progressInfo.IsCompleted ? "Complete" : "Ready");
        IsProcessing = _isProcessing;
        HasError = _progressInfo.Error != null;
        ErrorMessage = _progressInfo.Error?.Message ?? string.Empty;
    }

    private void CalculateStatistics()
    {
        NodeBufferSizeMB = (NodeBufferData?.Length ?? 0) / (1024f * 1024f);
        IndexBufferSizeMB = (IndexBufferData?.Length ?? 0) / (1024f * 1024f);
        TotalSizeMB = NodeBufferSizeMB + IndexBufferSizeMB;
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

    /// <summary>
    /// Releases CPU data to free memory. Call after uploading to GPU.
    /// </summary>
    public void ReleaseCpuData()
    {
        PlyData = new Dictionary<string, float[]>(0);
        VertexCount = 0;
        FieldOrder = Array.Empty<string>();
        NodeBufferData = Array.Empty<byte>();
        IndexBufferData = Array.Empty<byte>();
    }

    private void Log(string message)
    {
        if (Debug) Console.WriteLine(message);
    }
}
