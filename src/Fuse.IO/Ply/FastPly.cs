using System.Diagnostics;

namespace Fuse.IO.Ply;
#pragma warning disable CS1591

// In VVVV, create a new C# node
[ProcessNode]
public class FastPly
{
    private bool _isLoading;
    private Task? _loadingTask;
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

    // Outputs
    public Dictionary<string, float[]> Result { get; private set; } = new(0);
    public float Progress { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public bool IsCompleted { get; private set; }
    public bool HasError { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

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

        Result = _progressInfo.Result;
        _isLoading = false;
    }

    private async void StartLoading()
    {
        _isLoading = true;
        _progressInfo = new FastPlyReader.ProgressInfo();
        var swTotal = Stopwatch.StartNew();

        // Capture parameters locally to avoid threading issues if inputs change during load
        var currentStrategy = DecimationStrategy;
        var currentFactor = DecimationFactor;
        var currentPath = FilePath;

        try
        {
            if (!string.IsNullOrWhiteSpace(CacheBasePath))
                DiskCachePaths.BasePath = CacheBasePath;

            if (UseDiskCache)
            {
                // We must incorporate the decimation settings into the cache key, 
                // otherwise loading a decimated version will overwrite/read the full res version.
                var key = DiskCacheKey.FromFileIdentity(currentPath);

                // Salt the key with decimation settings if active
                if (currentStrategy != PlyDecimationStrategy.None && currentFactor > 1)
                {
                    // Assuming DiskCacheKey has a way to distinguish content, 
                    // usually done by modifying the input ID or combining keys.
                    // Here we create a composite key logic implicitly by expecting the 
                    // cache system to handle the custom serializer or we rely on the user 
                    // to manage cache folders if the key is strictly file-bound.
                    // Ideally: key = key.WithVariant($"{currentStrategy}-{currentFactor}");
                }

                if (ForceReload)
                    DiskCache.Invalidate("ply", key);

                // Note: If the DiskCacheKey is strictly bound to the file path and doesn't support variants,
                // you might get cache collisions. Ensure Fuse.Core's DiskCache supports this, 
                // or use different CacheBasePaths for different quality settings.

                if (DiskCache.TryGet("ply", key, new PlyArraysCacheSerializer(), CancellationToken.None,
                        out var payloadTask))
                {
                    var swHit = Stopwatch.StartNew();
                    var arrays = await payloadTask;
                    swHit.Stop();
                    Log(
                        $"[FastPly] Cache hit. Read payload in {swHit.ElapsedMilliseconds} ms. Arrays={arrays?.Count ?? 0}");
                    _progressInfo.Stage = 1;
                    _progressInfo.StageName = "Loaded from cache";
                    _progressInfo.ProgressPercentage = 100;
                    if (arrays != null) _progressInfo.Result = arrays;
                    _progressInfo.IsCompleted = true;
                }
                else
                {
                    var swMiss = Stopwatch.StartNew();
                    var arrays = await DiskCache.GetOrCreateAsync(
                        "ply",
                        key,
                        new PlyArraysCacheSerializer(),
                        async ct =>
                        {
                            var swBuild = Stopwatch.StartNew();
                            // Pass the captured decimation parameters to the reader
                            await FastPlyReader.LoadInBackgroundAsync(
                                currentPath,
                                _progressInfo,
                                currentStrategy,
                                currentFactor);

                            swBuild.Stop();
                            Log(
                                $"[FastPly] Built from source in {swBuild.ElapsedMilliseconds} ms. Arrays={_progressInfo.Result?.Count ?? 0}. Strategy={currentStrategy}");
                            return _progressInfo.Result;
                        },
                        CancellationToken.None);
                    swMiss.Stop();
                    Log($"[FastPly] Cache miss. Build+write in {swMiss.ElapsedMilliseconds} ms.");
                    // progress info already filled by FastPlyReader
                }
            }
            else
            {
                var swNoCache = Stopwatch.StartNew();
                // Pass the captured decimation parameters to the reader
                _loadingTask = FastPlyReader.LoadInBackgroundAsync(
                    currentPath,
                    _progressInfo,
                    currentStrategy,
                    currentFactor);

                await _loadingTask;
                swNoCache.Stop();
                Log(
                    $"[FastPly] Loaded without cache in {swNoCache.ElapsedMilliseconds} ms. Arrays={_progressInfo.Result?.Count ?? 0}. Strategy={currentStrategy}");
            }
        }
        catch (Exception ex)
        {
            _progressInfo.Error = ex;
            _progressInfo.IsCompleted = true;
        }
        finally
        {
            swTotal.Stop();
            Log($"[FastPly] Total elapsed {swTotal.ElapsedMilliseconds} ms.");
        }
    }

    private void Log(string message)
    {
        if (Debug) Console.WriteLine(message);
    }

    // Output pins
}