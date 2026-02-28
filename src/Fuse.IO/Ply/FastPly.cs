namespace Fuse.IO.Ply;
using Stride.Graphics;
using Stride.Core.Mathematics;

#pragma warning disable CS1591

/// <summary>
/// Process node that loads PLY files into a Struct-of-Arrays (SoA) layout.
/// Each field is stored in its own float[] array: x[], y[], z[], r[], g[], b[], etc.
///
/// This is the original FastPly loader with optimized parallel loading, decimation,
/// and disk caching support.
/// </summary>
[ProcessNode]
public class FastPly : ProcessNodeBase
{
    private bool _isLoading;
    private FastPlyReader.ProgressInfo? _progressInfo;
    private CancellationTokenSource? _loadCts;
    private int _updateCount;
    private int _triggerCount;
    private bool _lastLoad;

    public FastPly() : base("FastPly")
    {
        _loadCts = new CancellationTokenSource();
    }

    // Inputs
    public string FilePath { private get; set; } = string.Empty;
    public bool Load { private get; set; }
    public PlyDecimationStrategy DecimationStrategy { private get; set; } = PlyDecimationStrategy.None;
    public int DecimationFactor { private get; set; } = 1;

    public bool UseDiskCache { private get; set; }
    public bool ForceReload { private get; set; }
    public string CacheBasePath { private get; set; } = string.Empty;
    public PlyDataMode DataMode { private get; set; } = PlyDataMode.CpuOnly;
    public PlyBoundingBoxMode BoundingBoxMode { private get; set; } = PlyBoundingBoxMode.Auto;
    public GraphicsDevice? GraphicsDevice { private get; set; }
    public bool Debug { private get; set; }

    // Outputs - SoA data
    /// <summary>
    /// Dictionary of field name to float array. Each field is stored separately.
    /// For example: Result["x"] contains all X coordinates, Result["red"] contains all red values.
    /// </summary>
    public Dictionary<string, float[]> Result { get; private set; } = new(0);

    /// <summary>
    /// Number of vertices in the loaded point cloud.
    /// </summary>
    public int VertexCount { get; private set; }

    /// <summary>
    /// Names of fields in order as they appear in the PLY header.
    /// For example: ["x", "y", "z", "red", "green", "blue"]
    /// </summary>
    public string[] FieldOrder { get; private set; } = Array.Empty<string>();
    public PlyGpuData PlyGpuData { get; private set; } = PlyGpuData.Empty;
    public BoundingBox BoundingBox { get; private set; }
    public bool HasBoundingBox { get; private set; }

    // Outputs - progress/status
    public float Progress { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public bool IsCompleted { get; private set; }
    public bool HasError { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Returns this instance for chaining or downstream reference.
    /// </summary>
    public FastPly Output => this;

    public void Update()
    {
        UpdateDiagnostics(Debug);
        if (IsDisposed) return;
        _updateCount++;

        // Rising edge detection for Load trigger
        var loadRisingEdge = IsRisingEdge(ref _lastLoad, Load);

        if (Debug && Load != _lastLoad)
        {
            WriteDiagnostic(
                $"Load changed: {_lastLoad} -> {Load}, risingEdge={loadRisingEdge}, _isLoading={_isLoading}, IsCompleted={IsCompleted}, FilePath={(string.IsNullOrEmpty(FilePath) ? "<empty>" : Path.GetFileName(FilePath))}, frame={_updateCount}");
        }
        // Start loading on rising edge only
        if (loadRisingEdge && !_isLoading && !string.IsNullOrEmpty(FilePath))
        {
            _triggerCount++;
            if (Debug)
                WriteDiagnostic(
                    $"TRIGGER #{_triggerCount} - risingEdge detected, file={Path.GetFileName(FilePath)}, frame={_updateCount}");
            StartLoading();
        }

        // Update outputs with current progress
        if (_progressInfo == null) return;

        Progress = (float)_progressInfo.ProgressPercentage;
        Status = _progressInfo.StageName ?? Status;
        // NOTE: IsCompleted is set in StartLoading AFTER Result is populated, not from progressInfo
        HasError = _progressInfo.Error != null;
        ErrorMessage = _progressInfo.Error?.Message ?? string.Empty;

        if (!IsCompleted) return;
        if (DataMode != PlyDataMode.CpuOnly)
        {
            PlyGpuData.EnsureBuffers(GraphicsDevice);
        }

        // Result, VertexCount, and FieldOrder are set in StartLoading after load completes
        if (Debug)
            WriteDiagnostic(
                $"Load complete, _isLoading reset. Load pin={Load}, frame={_updateCount}");
        _isLoading = false;
    }

    private async void StartLoading()
    {
        var run = BeginRun(ref _loadCts);
        var generation = run.generation;
        var ct = run.token;
        _isLoading = true;
        IsCompleted = false; // Reset completion flag at start
        HasError = false;
        HasBoundingBox = false;
        BoundingBox = default;
        ErrorMessage = string.Empty;
        _progressInfo = new FastPlyReader.ProgressInfo();

        // Capture parameters locally to avoid threading issues if inputs change during load
        var currentStrategy = DecimationStrategy;
        var currentFactor = DecimationFactor;
        var currentPath = FilePath;
        var currentDataMode = DataMode;
        var currentBoundingBoxMode = BoundingBoxMode;

        try
        {
            // Use the shared PlyLoadCore helper for loading and caching
            var loadResult = await PlyLoadCore.LoadSoAAsync(
                currentPath,
                _progressInfo,
                currentStrategy,
                currentFactor,
                UseDiskCache,
                ForceReload,
                CacheBasePath,
                Debug,
                ct);

            if (!IsCurrentGeneration(generation)) return;

            // Update outputs from load result - set Result BEFORE IsCompleted!
            Result = loadResult.Arrays;
            VertexCount = loadResult.VertexCount;
            FieldOrder = loadResult.FieldOrder;
            TryUpdateBoundingBoxFromSoA(currentBoundingBoxMode, currentDataMode, loadResult.Arrays);
            PlyGpuData.DisposeBuffers();
            PlyGpuData = (currentDataMode == PlyDataMode.GpuOnly || currentDataMode == PlyDataMode.CpuAndGpu)
                ? new PlyGpuData(
                    PlyGpuDataFactory.CreatePlyFieldBuffers(Result, FieldOrder),
                    new Dictionary<string, GpuBufferInfo>(0),
                    VertexCount,
                    FieldOrder)
                : PlyGpuData.Empty;
            _progressInfo.Result = loadResult.Arrays;

            if (currentDataMode == PlyDataMode.GpuOnly)
            {
                // Try to materialize buffers immediately so upload providers can release CPU references.
                PlyGpuData.EnsureBuffers(GraphicsDevice);
            }

            if (currentDataMode == PlyDataMode.GpuOnly)
            {
                Result = new Dictionary<string, float[]>(0);
                _progressInfo.Result = new Dictionary<string, float[]>(0);
            }

            // NOW set IsCompleted - after Result is populated
            IsCompleted = true;
            TrackAllocationEstimate("load-complete", EstimateRetainedBytes());

            Log($"[FastPly] Load complete. Arrays={loadResult.Arrays.Count}, Vertices={loadResult.VertexCount}, CacheHit={loadResult.WasCacheHit}");
        }
        catch (Exception ex)
        {
            if (!IsCurrentGeneration(generation)) return;
            if (ex is OperationCanceledException)
            {
                _isLoading = false;
                return;
            }
            _progressInfo.Error = ex;
            _progressInfo.IsCompleted = true;
            IsCompleted = true; // Also set on error
            HasError = true;
            ErrorMessage = ex.Message;
        }
    }

    private void ClearAllRetainedData(string reason)
    {
        TrackReleaseEstimate(reason, EstimateRetainedBytes, () =>
        {
            PlyGpuData.DisposeBuffers();
            Result = new Dictionary<string, float[]>(0);
            VertexCount = 0;
            FieldOrder = Array.Empty<string>();
            PlyGpuData = PlyGpuData.Empty;
            BoundingBox = default;
            HasBoundingBox = false;
            if (_progressInfo != null)
                _progressInfo.Result = new Dictionary<string, float[]>(0);
        });
    }

    protected override void OnDisposeManaged()
    {
        _isLoading = false;
        CancelAndDisposeCts(ref _loadCts);
        ClearAllRetainedData("Dispose");
        _progressInfo = null;
        Progress = 0;
        Status = "Disposed";
        IsCompleted = false;
        HasError = false;
        ErrorMessage = string.Empty;
    }

    private void Log(string message)
    {
        if (Debug) Console.WriteLine(message);
    }

    private long EstimateRetainedBytes()
    {
        long bytes = 0;
        if (Result != null)
        {
            foreach (var arr in Result.Values)
            {
                if (arr != null)
                    bytes += (long)arr.Length * sizeof(float);
            }
        }
        if (_progressInfo?.Result != null)
        {
            foreach (var arr in _progressInfo.Result.Values)
            {
                if (arr != null)
                    bytes += (long)arr.Length * sizeof(float);
            }
        }
        if (PlyGpuData != null)
        {
            bytes += PlyGpuData.GetEstimatedRetainedBytes();
        }
        return bytes;
    }

    private void TryUpdateBoundingBoxFromSoA(PlyBoundingBoxMode mode, PlyDataMode dataMode, Dictionary<string, float[]> arrays)
    {
        if (mode == PlyBoundingBoxMode.CpuVariantsOnly && dataMode == PlyDataMode.GpuOnly)
        {
            HasBoundingBox = false;
            BoundingBox = default;
            return;
        }

        if (mode == PlyBoundingBoxMode.Off || mode == PlyBoundingBoxMode.OctreeRoot)
        {
            HasBoundingBox = false;
            BoundingBox = default;
            return;
        }

        HasBoundingBox = PlyBoundingBoxUtils.TryComputeFromSoA(arrays, out var bbox);
        BoundingBox = bbox;
    }
}

