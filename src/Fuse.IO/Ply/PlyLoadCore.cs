using System.Diagnostics;

namespace Fuse.IO.Ply;

#pragma warning disable CS1591

/// <summary>
/// Internal helper that encapsulates the shared PLY loading and caching logic
/// used by both FastPly (SoA) and FastPlyInterleaved (AoS) nodes.
/// </summary>
internal static class PlyLoadCore
{
    /// <summary>
    /// Result of loading PLY data, including the SoA dictionary, field order from header,
    /// and metadata about the load operation.
    /// </summary>
    public class LoadResult
    {
        public Dictionary<string, float[]> Arrays { get; set; } = new(0);
        public string[] FieldOrder { get; set; } = Array.Empty<string>();
        public int VertexCount { get; set; }
        public bool WasCacheHit { get; set; }
    }

    /// <summary>
    /// Loads PLY data using disk cache when enabled, returning decimated SoA arrays.
    /// This is the shared core used by both FastPly and FastPlyInterleaved nodes.
    /// </summary>
    public static async Task<LoadResult> LoadSoAAsync(
        string filePath,
        FastPlyReader.ProgressInfo progressInfo,
        PlyDecimationStrategy decimationStrategy,
        int decimationFactor,
        bool useDiskCache,
        bool forceReload,
        string cacheBasePath,
        bool debug)
    {
        var result = new LoadResult();
        var swTotal = Stopwatch.StartNew();

        try
        {
            if (!string.IsNullOrWhiteSpace(cacheBasePath))
                DiskCachePaths.BasePath = cacheBasePath;

            if (useDiskCache)
            {
                // Cache key includes decimation settings so different quality levels get separate cache entries
                var key = DiskCacheKey.ForPly(filePath, decimationStrategy, decimationFactor);
                Log(debug, $"[PlyLoadCore] Cache key: {key}");

                async Task<Dictionary<string, float[]>> BuildFromSourceAsync(CancellationToken ct)
                {
                    var swBuild = Stopwatch.StartNew();
                    await FastPlyReader.LoadInBackgroundAsync(
                        filePath,
                        progressInfo,
                        decimationStrategy,
                        decimationFactor);
                    swBuild.Stop();

                    if (progressInfo.Error != null)
                        throw progressInfo.Error;

                    var builtArrays = progressInfo.Result ?? new Dictionary<string, float[]>(0);
                    var builtVertexCount = builtArrays.Count > 0 ? builtArrays.Values.First().Length : 0;
                    Log(debug,
                        $"[PlyLoadCore] Built from source in {swBuild.ElapsedMilliseconds} ms. Arrays={builtArrays.Count}, Vertices={builtVertexCount}. Strategy={decimationStrategy}");

                    if (progressInfo.TotalVertices > 0 && (builtArrays.Count == 0 || builtVertexCount == 0))
                    {
                        throw new InvalidOperationException(
                            $"PLY load produced empty output for non-empty source (SourceVertices={progressInfo.TotalVertices}).");
                    }

                    return builtArrays;
                }

                if (forceReload)
                {
                    Log(debug, "[PlyLoadCore] ForceReload=true, invalidating cache");
                    DiskCache.Invalidate("ply", key);
                }

                if (DiskCache.TryGet("ply", key, new PlyArraysCacheSerializer(), CancellationToken.None,
                        out var payloadTask))
                {
                    var swHit = Stopwatch.StartNew();
                    var arrays = await payloadTask;
                    swHit.Stop();
                    
                    var arrayCount = arrays?.Count ?? 0;
                    var vertexCount = (arrays != null && arrays.Count > 0) ? arrays.Values.First().Length : 0;
                    Log(debug,
                        $"[PlyLoadCore] Cache hit. Read payload in {swHit.ElapsedMilliseconds} ms. Arrays={arrayCount}, Vertices={vertexCount}");
                    
                    // Validate cache integrity - if cache has 0 arrays/vertices but file exists, it's corrupted
                    if (arrayCount == 0 || vertexCount == 0)
                    {
                        Console.WriteLine(
                            $"[PlyLoadCore] WARNING: Cache returned empty data (Arrays={arrayCount}, Vertices={vertexCount}). Invalidating and rebuilding from source.");
                        DiskCache.Invalidate("ply", key);
                        arrays = await DiskCache.GetOrCreateAsync(
                            "ply",
                            key,
                            new PlyArraysCacheSerializer(),
                            BuildFromSourceAsync,
                            CancellationToken.None);
                        arrayCount = arrays?.Count ?? 0;
                        vertexCount = (arrays != null && arrays.Count > 0) ? arrays.Values.First().Length : 0;
                        Log(debug,
                            $"[PlyLoadCore] Cache recovered. Arrays={arrayCount}, Vertices={vertexCount}");
                    }

                    if (arrays != null)
                    {
                        progressInfo.Result = arrays;
                        result.Arrays = arrays;
                        // When loading from cache, field order comes from dictionary key order
                        // (preserved by .NET 8 Dictionary and our cache serializer)
                        result.FieldOrder = arrays.Keys.ToArray();
                        progressInfo.FieldOrder = result.FieldOrder;
                        result.VertexCount = arrays.Count > 0 ? arrays.Values.First().Length : 0;
                    }

                    progressInfo.Stage = 1;
                    progressInfo.StageName = "Loaded from cache";
                    progressInfo.ProgressPercentage = 100;
                    progressInfo.IsCompleted = true;
                    result.WasCacheHit = arrayCount > 0 && vertexCount > 0;
                }
                else
                {
                    var swMiss = Stopwatch.StartNew();
                    var arrays = await DiskCache.GetOrCreateAsync(
                        "ply",
                        key,
                        new PlyArraysCacheSerializer(),
                        BuildFromSourceAsync,
                        CancellationToken.None);
                    swMiss.Stop();
                    
                    var cachedVertexCount = (arrays != null && arrays.Count > 0) ? arrays.Values.First().Length : 0;
                    Log(debug, $"[PlyLoadCore] Cache miss. Build+write in {swMiss.ElapsedMilliseconds} ms. Cached Arrays={arrays?.Count ?? 0}, Vertices={cachedVertexCount}");
                    
                    result.Arrays = arrays ?? new Dictionary<string, float[]>(0);
                    // Use field order from FastPlyReader (preserves PLY header order)
                    result.FieldOrder = progressInfo.FieldOrder.Length > 0 
                        ? progressInfo.FieldOrder 
                        : result.Arrays.Keys.ToArray();
                    result.VertexCount = result.Arrays.Count > 0 ? result.Arrays.Values.First().Length : 0;
                    result.WasCacheHit = false;
                    // progress info already filled by FastPlyReader
                }
            }
            else
            {
                var swNoCache = Stopwatch.StartNew();
                // Pass the decimation parameters to the reader
                await FastPlyReader.LoadInBackgroundAsync(
                    filePath,
                    progressInfo,
                    decimationStrategy,
                    decimationFactor);

                if (progressInfo.Error != null)
                    throw progressInfo.Error;

                swNoCache.Stop();
                Log(debug,
                    $"[PlyLoadCore] Loaded without cache in {swNoCache.ElapsedMilliseconds} ms. Arrays={progressInfo.Result?.Count ?? 0}. Strategy={decimationStrategy}");
                
                var loadedArrays = progressInfo.Result ?? new Dictionary<string, float[]>(0);
                result.Arrays = loadedArrays;
                // Use field order from FastPlyReader (preserves PLY header order)
                result.FieldOrder = progressInfo.FieldOrder.Length > 0 
                    ? progressInfo.FieldOrder 
                    : loadedArrays.Keys.ToArray();
                result.VertexCount = loadedArrays.Count > 0 ? loadedArrays.Values.First().Length : 0;
                result.WasCacheHit = false;
            }
        }
        catch (Exception ex)
        {
            progressInfo.Error = ex;
            progressInfo.IsCompleted = true;
            throw;
        }
        finally
        {
            swTotal.Stop();
            Log(debug, $"[PlyLoadCore] Total elapsed {swTotal.ElapsedMilliseconds} ms.");
        }

        return result;
    }

    private static void Log(bool debug, string message)
    {
        if (debug) Console.WriteLine(message);
    }
}

