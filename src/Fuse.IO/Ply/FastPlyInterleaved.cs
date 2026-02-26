using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Stride.Graphics;

namespace Fuse.IO.Ply;

#pragma warning disable CS1591

/// <summary>
/// Process node that loads PLY files into an interleaved Array-of-Structs (AoS) layout.
/// All PLY fields are stored in subsequent order in one big array:
/// [x0, y0, z0, r0, g0, b0, x1, y1, z1, r1, g1, b1, ...]
/// 
/// This node mirrors the behavior of FastPly but outputs data in AoS format instead of SoA.
/// It reuses the same caching, decimation, and performance optimizations as FastPly.
/// </summary>
[ProcessNode]
public class FastPlyInterleaved : ProcessNodeBase
{
    private bool _isLoading;
    private FastPlyReader.ProgressInfo? _progressInfo;
    private CancellationTokenSource? _loadCts;
    private int _updateCount;
    private int _triggerCount;
    private bool _lastLoad;

    public FastPlyInterleaved() : base("FastPlyInterleaved")
    {
        _loadCts = new CancellationTokenSource();
    }

    // Inputs - mirror FastPly exactly
    public string FilePath { get; set; } = string.Empty;
    public bool Load { get; set; }
    public PlyDecimationStrategy DecimationStrategy { get; set; } = PlyDecimationStrategy.None;
    public int DecimationFactor { get; set; } = 1;

    public bool UseDiskCache { get; set; }
    public bool ForceReload { get; set; }
    public string CacheBasePath { get; set; } = string.Empty;
    public PlyDataMode DataMode { get; set; } = PlyDataMode.CpuOnly;
    public GraphicsDevice? GraphicsDevice { get; set; }
    public bool Debug { get; set; }

    // Outputs - AoS specific
    /// <summary>
    /// The interleaved result array containing all vertices with all fields in sequence:
    /// [x0, y0, z0, r0, g0, b0, x1, y1, z1, r1, g1, b1, ...]
    /// </summary>
    public float[] Result { get; private set; } = Array.Empty<float>();
    
    /// <summary>
    /// Number of vertices in the loaded point cloud.
    /// </summary>
    public int VertexCount { get; private set; }
    
    /// <summary>
    /// Number of fields per vertex (e.g., 6 for x, y, z, r, g, b).
    /// Result.Length == VertexCount * FieldsPerVertex
    /// </summary>
    public int FieldsPerVertex { get; private set; }
    
    /// <summary>
    /// Names of fields in order as they appear in each vertex struct.
    /// For example: ["x", "y", "z", "red", "green", "blue"]
    /// </summary>
    public string[] FieldOrder { get; private set; } = Array.Empty<string>();
    public PlyGpuData PlyGpuData { get; private set; } = PlyGpuData.Empty;

    // Outputs - progress/status (same as FastPly)
    public float Progress { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public bool IsCompleted { get; private set; }
    public bool HasError { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    /// <summary>
    /// Returns this instance for chaining or downstream reference.
    /// </summary>
    public FastPlyInterleaved Output => this;

    public void Update()
    {
        UpdateDiagnostics(Debug, GetCreationCallsite);
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

        // Result is set in StartLoading after conversion
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
        ErrorMessage = string.Empty;
        _progressInfo = new FastPlyReader.ProgressInfo();

        // Capture parameters locally to avoid threading issues if inputs change during load
        var currentStrategy = DecimationStrategy;
        var currentFactor = DecimationFactor;
        var currentPath = FilePath;
        var currentDataMode = DataMode;

        try
        {
            // Use the shared PlyLoadCore helper for loading and caching (SoA format)
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

            // Convert SoA to AoS (interleaved) once. GPU mode uses one interleaved float buffer.
            _progressInfo.StageName = "Packing interleaved buffer";
            var swConvert = Stopwatch.StartNew();
            var (interleaved, vertexCount, fieldsPerVertex, fieldOrder) =
                ConvertSoAToInterleaved(loadResult.Arrays, loadResult.FieldOrder);
            swConvert.Stop();
            var convertMs = swConvert.ElapsedMilliseconds;

            Result = interleaved;
            VertexCount = vertexCount;
            FieldsPerVertex = fieldsPerVertex;
            FieldOrder = fieldOrder;

            PlyGpuData.DisposeBuffers();
            PlyGpuData = (currentDataMode == PlyDataMode.GpuOnly || currentDataMode == PlyDataMode.CpuAndGpu)
                ? new PlyGpuData(
                    PlyGpuDataFactory.CreateInterleavedFloatBuffer(interleaved),
                    new Dictionary<string, GpuBufferInfo>(0),
                    VertexCount,
                    FieldOrder)
                : PlyGpuData.Empty;

            if (currentDataMode == PlyDataMode.GpuOnly)
            {
                PlyGpuData.EnsureBuffers(GraphicsDevice);
                Result = Array.Empty<float>();
            }

            // Interleaved output no longer needs the temporary SoA arrays.
            // Keeping them doubles peak retained memory for this node.
            _progressInfo.Result = new Dictionary<string, float[]>(0);

            _progressInfo.StageName = "Complete";
            _progressInfo.ProgressPercentage = 100;
            _progressInfo.IsCompleted = true;
            
            // NOW set IsCompleted - after Result is populated
            IsCompleted = true;
            var retainedBytes = EstimateRetainedBytes();
            TrackAllocationEstimate("load-complete", retainedBytes);

            Log($"[FastPlyInterleaved] Load complete. Vertices={VertexCount}, Fields={FieldsPerVertex}, " +
                $"InterleavedSize={Result.Length}, ConvertTime={convertMs}ms, " +
                $"CacheHit={loadResult.WasCacheHit}");
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

    /// <summary>
    /// Converts SoA (Struct of Arrays) data to AoS (Array of Structs) interleaved format.
    /// High-performance implementation that pre-resolves array references and uses
    /// cache-friendly access patterns.
    /// </summary>
    private static (float[] interleaved, int vertexCount, int fieldsPerVertex, string[] fieldOrder) 
        ConvertSoAToInterleaved(Dictionary<string, float[]> soaArrays, string[] fieldOrder)
    {
        if (soaArrays.Count == 0 || fieldOrder.Length == 0)
            return (Array.Empty<float>(), 0, 0, Array.Empty<string>());

        var fieldsPerVertex = fieldOrder.Length;
        
        // Pre-resolve field arrays to avoid dictionary lookups in the hot loop
        var fieldArrays = new float[fieldsPerVertex][];
        for (var f = 0; f < fieldsPerVertex; f++)
        {
            if (!soaArrays.TryGetValue(fieldOrder[f], out var arr))
                throw new InvalidOperationException($"Field '{fieldOrder[f]}' not found in SoA arrays");
            fieldArrays[f] = arr;
        }

        // Validate all fields have the same length
        var vertexCount = fieldArrays[0].Length;
        for (var f = 1; f < fieldsPerVertex; f++)
        {
            if (fieldArrays[f].Length != vertexCount)
                throw new InvalidOperationException(
                    $"Field '{fieldOrder[f]}' has {fieldArrays[f].Length} elements, expected {vertexCount}");
        }

        // Allocate the interleaved array
        var totalFloats = (long)vertexCount * fieldsPerVertex;
        if (totalFloats > int.MaxValue)
            throw new InvalidOperationException($"Interleaved array would exceed maximum size: {totalFloats} floats");
        
        var interleaved = new float[totalFloats];

        // Choose conversion strategy based on data size
        const int parallelThreshold = 100_000; // Use parallel conversion for large datasets
        
        if (vertexCount >= parallelThreshold)
        {
            ConvertParallel(fieldArrays, interleaved, vertexCount, fieldsPerVertex);
        }
        else
        {
            ConvertSequential(fieldArrays, interleaved, vertexCount, fieldsPerVertex);
        }

        return (interleaved, vertexCount, fieldsPerVertex, fieldOrder);
    }

    /// <summary>
    /// Sequential conversion - optimal for smaller datasets where parallelization overhead
    /// would outweigh benefits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConvertSequential(float[][] fieldArrays, float[] interleaved, int vertexCount, int fieldsPerVertex)
    {
        var writeIdx = 0;
        for (var v = 0; v < vertexCount; v++)
        {
            for (var f = 0; f < fieldsPerVertex; f++)
            {
                interleaved[writeIdx++] = fieldArrays[f][v];
            }
        }
    }

    /// <summary>
    /// Parallel conversion - splits work across CPU cores for large datasets.
    /// Each worker processes a contiguous range of vertices.
    /// </summary>
    private static void ConvertParallel(float[][] fieldArrays, float[] interleaved, int vertexCount, int fieldsPerVertex)
    {
        var processorCount = Environment.ProcessorCount;
        var verticesPerWorker = (vertexCount + processorCount - 1) / processorCount;

        Parallel.For(0, processorCount, workerId =>
        {
            var startVertex = workerId * verticesPerWorker;
            var endVertex = Math.Min(startVertex + verticesPerWorker, vertexCount);
            
            var writeIdx = startVertex * fieldsPerVertex;
            for (var v = startVertex; v < endVertex; v++)
            {
                for (var f = 0; f < fieldsPerVertex; f++)
                {
                    interleaved[writeIdx++] = fieldArrays[f][v];
                }
            }
        });
    }

    private void ClearAllRetainedData(string reason)
    {
        TrackReleaseEstimate(reason, EstimateRetainedBytes, () =>
        {
            PlyGpuData.DisposeBuffers();
            Result = Array.Empty<float>();
            VertexCount = 0;
            FieldsPerVertex = 0;
            FieldOrder = Array.Empty<string>();
            PlyGpuData = PlyGpuData.Empty;
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
            bytes += (long)Result.Length * sizeof(float);
        if (_progressInfo?.Result != null)
        {
            foreach (var arr in _progressInfo.Result.Values)
            {
                if (arr != null)
                    bytes += (long)arr.Length * sizeof(float);
            }
        }
        if (PlyGpuData != null)
            bytes += PlyGpuData.GetEstimatedRetainedBytes();
        return bytes;
    }

    private static string GetCreationCallsite()
    {
        try
        {
            var st = new StackTrace(skipFrames: 2, fNeedFileInfo: true);
            var sb = new StringBuilder();
            var appended = 0;
            for (var i = 0; i < st.FrameCount && appended < 5; i++)
            {
                var frame = st.GetFrame(i);
                var method = frame?.GetMethod();
                var typeName = method?.DeclaringType?.FullName ?? "<unknown>";
                if (typeName.StartsWith("System.", StringComparison.Ordinal) ||
                    typeName.StartsWith("Microsoft.", StringComparison.Ordinal))
                    continue;

                if (appended > 0) sb.Append(" <= ");
                var methodName = method?.Name ?? "<unknown>";
                var file = frame?.GetFileName();
                var line = frame?.GetFileLineNumber() ?? 0;
                if (!string.IsNullOrWhiteSpace(file) && line > 0)
                    sb.Append($"{typeName}.{methodName}({Path.GetFileName(file)}:{line})");
                else
                    sb.Append($"{typeName}.{methodName}");
                appended++;
            }

            return appended > 0 ? sb.ToString() : "callsite-unavailable";
        }
        catch
        {
            return "callsite-error";
        }
    }
}



