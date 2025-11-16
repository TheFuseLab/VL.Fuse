using System;
using System.Diagnostics;
namespace VL.E57;
#pragma warning disable CS1591

// In VVVV, create a new C# node
[ProcessNode]
public class FastPly
{
    private bool _isLoading;
    private Task? _loadingTask;
    private FastPlyReader.ProgressInfo? _progressInfo;

    public string FilePath { get; set; } = string.Empty;
    public bool Load { get; set; }
    public bool UseDiskCache { get; set; }
    public bool ForceReload { get; set; }
    public string CacheBasePath { get; set; } = string.Empty;
    public bool Debug { get; set; }

    // Output pins
    public Dictionary<string, float[]> Result { get; private set; } = new Dictionary<string, float[]>(0);
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

        try
        {
            if (!string.IsNullOrWhiteSpace(CacheBasePath))
                DiskCachePaths.BasePath = CacheBasePath;

            if (UseDiskCache)
            {
                var key = DiskCacheKey.FromFileIdentity(FilePath);
                if (ForceReload)
                    DiskCache.Invalidate("ply", key);

                if (DiskCache.TryGet("ply", key, new PlyArraysCacheSerializer(), CancellationToken.None, out var payloadTask))
                {
                    var swHit = Stopwatch.StartNew();
                    var arrays = await payloadTask;
                    swHit.Stop();
                    Log($"[FastPly] Cache hit. Read payload in {swHit.ElapsedMilliseconds} ms. Arrays={arrays?.Count ?? 0}");
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
                        cacheNamespace: "ply",
                        key: key,
                        serializer: new PlyArraysCacheSerializer(),
                        buildAsync: async ct =>
                        {
                            var swBuild = Stopwatch.StartNew();
                            await FastPlyReader.LoadInBackgroundAsync(FilePath, _progressInfo);
                            swBuild.Stop();
                            Log($"[FastPly] Built from source in {swBuild.ElapsedMilliseconds} ms. Arrays={_progressInfo.Result?.Count ?? 0}");
                            return _progressInfo.Result;
                        },
                        ct: CancellationToken.None);
                    swMiss.Stop();
                    Log($"[FastPly] Cache miss. Build+write in {swMiss.ElapsedMilliseconds} ms.");
                    // progress info already filled by FastPlyReader
                }
            }
            else
            {
                var swNoCache = Stopwatch.StartNew();
                _loadingTask = FastPlyReader.LoadInBackgroundAsync(FilePath, _progressInfo);
                await _loadingTask;
                swNoCache.Stop();
                Log($"[FastPly] Loaded without cache in {swNoCache.ElapsedMilliseconds} ms. Arrays={_progressInfo.Result?.Count ?? 0}");
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
}