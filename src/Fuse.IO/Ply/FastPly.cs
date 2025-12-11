namespace Fuse.IO.Ply;

#pragma warning disable CS1591

/// <summary>
/// Process node that loads PLY files into a Struct-of-Arrays (SoA) layout.
/// Each field is stored in its own float[] array: x[], y[], z[], r[], g[], b[], etc.
/// 
/// This is the original FastPly loader with optimized parallel loading, decimation,
/// and disk caching support.
/// </summary>
[ProcessNode]
public class FastPly
{
    private bool _isLoading;
    private FastPlyReader.ProgressInfo? _progressInfo;

    // Inputs
    public string FilePath { get; set; } = string.Empty;
    public bool Load { get; set; }
    public PlyDecimationStrategy DecimationStrategy { get; set; } = PlyDecimationStrategy.None;
    public int DecimationFactor { get; set; } = 1;

    public bool UseDiskCache { get; set; }
    public bool ForceReload { get; set; }
    public string CacheBasePath { get; set; } = string.Empty;
    public bool Debug { get; set; }

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
        // Start loading when Load is triggered
        if (Load && !_isLoading && !string.IsNullOrEmpty(FilePath)) StartLoading();

        // Update outputs with current progress
        if (_progressInfo == null) return;

        Progress = (float)_progressInfo.ProgressPercentage;
        Status = _progressInfo.StageName ?? Status;
        IsCompleted = _progressInfo.IsCompleted;
        HasError = _progressInfo.Error != null;
        ErrorMessage = _progressInfo.Error?.Message ?? string.Empty;

        if (!_progressInfo.IsCompleted) return;

        // Result, VertexCount, and FieldOrder are set in StartLoading after load completes
        _isLoading = false;
    }

    private async void StartLoading()
    {
        _isLoading = true;
        _progressInfo = new FastPlyReader.ProgressInfo();

        // Capture parameters locally to avoid threading issues if inputs change during load
        var currentStrategy = DecimationStrategy;
        var currentFactor = DecimationFactor;
        var currentPath = FilePath;

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
                Debug);

            // Update outputs from load result
            Result = loadResult.Arrays;
            VertexCount = loadResult.VertexCount;
            FieldOrder = loadResult.FieldOrder;
            _progressInfo.Result = loadResult.Arrays;
            
            Log($"[FastPly] Load complete. Arrays={loadResult.Arrays.Count}, Vertices={loadResult.VertexCount}, CacheHit={loadResult.WasCacheHit}");
        }
        catch (Exception ex)
        {
            _progressInfo.Error = ex;
            _progressInfo.IsCompleted = true;
        }
    }
    
    /// <summary>
    /// Releases CPU data to free memory. Call after uploading to GPU.
    /// </summary>
    public void ReleaseCpuData()
    {
        Result = new Dictionary<string, float[]>(0);
        VertexCount = 0;
        FieldOrder = Array.Empty<string>();
        if (_progressInfo != null)
        {
            _progressInfo.Result = new Dictionary<string, float[]>(0);
        }
    }

    private void Log(string message)
    {
        if (Debug) Console.WriteLine(message);
    }

    // Output pins
}