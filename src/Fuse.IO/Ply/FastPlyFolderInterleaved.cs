using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Fuse.IO.Ply;

#pragma warning disable CS1591

/// <summary>
/// Process node that loads all compatible PLY files from a folder and composes them
/// into one interleaved Array-of-Structs (AoS) buffer.
/// </summary>
[ProcessNode]
public class FastPlyFolderInterleaved
{
    private bool _isLoading;
    private bool _lastLoadTrigger;
    private FastPlyReader.ProgressInfo? _fileProgressInfo;
    private int _fileCount;
    private int _filesCompleted;
    private int _phase;

    // Inputs
    public string FolderPath { get; set; } = string.Empty;
    public string SearchPattern { get; set; } = "*.ply";
    public bool Recursive { get; set; }
    public bool Load { get; set; }

    public bool AddCloudId { get; set; }
    public string CloudIdFieldName { get; set; } = "cloud_id";

    public PlyDecimationStrategy DecimationStrategy { get; set; } = PlyDecimationStrategy.None;
    public int DecimationFactor { get; set; } = 1;

    public bool UseDiskCache { get; set; }
    public bool ForceReload { get; set; }
    public string CacheBasePath { get; set; } = string.Empty;
    public bool Debug { get; set; }

    // Outputs - AoS data
    public float[] Result { get; private set; } = Array.Empty<float>();
    public int VertexCount { get; private set; }
    public int FieldsPerVertex { get; private set; }
    public string[] FieldOrder { get; private set; } = Array.Empty<string>();

    // Outputs - folder metadata
    public string[] LoadedFiles { get; private set; } = Array.Empty<string>();
    public int CloudCount { get; private set; }

    // Outputs - progress/status
    public float Progress { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string StageName { get; private set; } = string.Empty;
    public bool IsCompleted { get; private set; }
    public bool HasError { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    public FastPlyFolderInterleaved Output => this;

    public void Update()
    {
        var loadRisingEdge = Load && !_lastLoadTrigger;
        _lastLoadTrigger = Load;

        if (loadRisingEdge && !_isLoading && !string.IsNullOrWhiteSpace(FolderPath))
            StartLoading();

        if (!_isLoading || _phase != 1 || _fileCount <= 0)
            return;

        var currentFileProgress = _fileProgressInfo?.ProgressPercentage ?? 0.0;
        var normalizedLoadProgress = (_filesCompleted + currentFileProgress / 100.0) / _fileCount;
        Progress = (float)Math.Min(90.0, normalizedLoadProgress * 90.0);
        HasError = _fileProgressInfo?.Error != null;
        ErrorMessage = _fileProgressInfo?.Error?.Message ?? string.Empty;
    }

    private async void StartLoading()
    {
        _isLoading = true;
        _phase = 0;
        _fileCount = 0;
        _filesCompleted = 0;
        _fileProgressInfo = null;

        IsCompleted = false;
        HasError = false;
        ErrorMessage = string.Empty;
        Progress = 0;
        Status = "Loading";
        StageName = "Scanning folder";

        Result = Array.Empty<float>();
        VertexCount = 0;
        FieldsPerVertex = 0;
        FieldOrder = Array.Empty<string>();
        LoadedFiles = Array.Empty<string>();
        CloudCount = 0;

        var currentFolderPath = FolderPath;
        var currentSearchPattern = string.IsNullOrWhiteSpace(SearchPattern) ? "*.ply" : SearchPattern;
        var currentRecursive = Recursive;
        var currentAddCloudId = AddCloudId;
        var currentCloudIdFieldName = string.IsNullOrWhiteSpace(CloudIdFieldName) ? "cloud_id" : CloudIdFieldName;
        var currentStrategy = DecimationStrategy;
        var currentFactor = DecimationFactor;
        var currentUseDiskCache = UseDiskCache;
        var currentForceReload = ForceReload;
        var currentCacheBasePath = CacheBasePath;
        var currentDebug = Debug;

        var swTotal = Stopwatch.StartNew();

        try
        {
            var files = EnumeratePlyFiles(currentFolderPath, currentSearchPattern, currentRecursive);
            if (files.Length == 0)
                throw new FileNotFoundException(
                    $"No PLY files found in '{currentFolderPath}' with pattern '{currentSearchPattern}'.");

            LoadedFiles = files;
            CloudCount = files.Length;
            _fileCount = files.Length;

            Log(currentDebug, $"[FastPlyFolderInterleaved] Found {files.Length} PLY files.");

            StageName = "Validating headers";
            var headers = files.Select(ReadPlyHeader).ToArray();
            ValidateCompatibleHeaders(headers);

            var baseFieldOrder = headers[0].FieldOrder;
            if (currentAddCloudId &&
                baseFieldOrder.Any(x => string.Equals(x, currentCloudIdFieldName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Cannot add cloud id field '{currentCloudIdFieldName}' because a source PLY already contains it.");
            }

            var composedFieldOrder = currentAddCloudId
                ? baseFieldOrder.Concat(new[] { currentCloudIdFieldName }).ToArray()
                : baseFieldOrder.ToArray();
            var initialVertexCapacity = EstimateComposedVertexCapacity(headers, currentStrategy, currentFactor);

            _phase = 1;
            StageName = "Loading PLY files";

            var composedSoA = await LoadAndComposeSoAAsync(
                files,
                composedFieldOrder,
                baseFieldOrder,
                initialVertexCapacity,
                currentAddCloudId,
                currentCloudIdFieldName,
                currentStrategy,
                currentFactor,
                currentUseDiskCache,
                currentForceReload,
                currentCacheBasePath,
                currentDebug);

            _phase = 2;
            StageName = "Packing interleaved buffer";
            Progress = 95;

            var swConvert = Stopwatch.StartNew();
            var (interleaved, vertexCount, fieldsPerVertex, fieldOrder) =
                ConvertSoAToInterleaved(composedSoA, composedFieldOrder);
            swConvert.Stop();

            Result = interleaved;
            VertexCount = vertexCount;
            FieldsPerVertex = fieldsPerVertex;
            FieldOrder = fieldOrder;

            _phase = 3;
            StageName = "Complete";
            Status = "Complete";
            Progress = 100;
            IsCompleted = true;

            swTotal.Stop();
            Log(currentDebug,
                $"[FastPlyFolderInterleaved] Load complete. Files={files.Length}, Vertices={vertexCount:N0}, " +
                $"Fields={fieldsPerVertex}, InterleavedSize={interleaved.Length:N0}, " +
                $"ConvertTime={swConvert.ElapsedMilliseconds}ms, TotalTime={swTotal.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            Status = "Error";
            StageName = "Error";
            IsCompleted = true;
            Progress = 100;
            _fileProgressInfo ??= new FastPlyReader.ProgressInfo();
            _fileProgressInfo.Error = ex;
            _fileProgressInfo.IsCompleted = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task<Dictionary<string, float[]>> LoadAndComposeSoAAsync(
        string[] files,
        string[] composedFieldOrder,
        string[] sourceFieldOrder,
        int initialVertexCapacity,
        bool addCloudId,
        string cloudIdFieldName,
        PlyDecimationStrategy decimationStrategy,
        int decimationFactor,
        bool useDiskCache,
        bool forceReload,
        string cacheBasePath,
        bool debug)
    {
        var composed = new Dictionary<string, float[]>(composedFieldOrder.Length);
        if (initialVertexCapacity > 0)
        {
            foreach (var fieldName in composedFieldOrder)
                composed[fieldName] = new float[initialVertexCapacity];
        }

        var writeOffset = 0;

        for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            var file = files[fileIndex];
            _fileProgressInfo = new FastPlyReader.ProgressInfo();
            StageName = $"Loading {fileIndex + 1}/{files.Length}";
            Status = Path.GetFileName(file);

            Log(debug, $"[FastPlyFolderInterleaved] Loading {fileIndex + 1}/{files.Length}: {file}");

            var loadResult = await PlyLoadCore.LoadSoAAsync(
                file,
                _fileProgressInfo,
                decimationStrategy,
                decimationFactor,
                useDiskCache,
                forceReload,
                cacheBasePath,
                debug);

            if (_fileProgressInfo.Error != null)
                throw _fileProgressInfo.Error;

            ValidateLoadedFields(file, loadResult.Arrays, sourceFieldOrder);

            var vertexCount = loadResult.VertexCount;
            EnsureComposedCapacity(composed, composedFieldOrder, writeOffset + vertexCount);

            foreach (var fieldName in sourceFieldOrder)
            {
                var source = loadResult.Arrays[fieldName];
                Array.Copy(source, 0, composed[fieldName], writeOffset, source.Length);
            }

            if (addCloudId)
            {
                var cloudId = (float)fileIndex;
                composed[cloudIdFieldName].AsSpan(writeOffset, vertexCount).Fill(cloudId);
            }

            writeOffset += vertexCount;
            _filesCompleted = fileIndex + 1;
        }

        TrimComposedArrays(composed, writeOffset);
        return composed;
    }

    private static int EstimateComposedVertexCapacity(
        PlyHeaderInfo[] headers,
        PlyDecimationStrategy decimationStrategy,
        int decimationFactor)
    {
        if (headers.Length == 0)
            return 0;

        if (decimationFactor < 1)
            decimationFactor = 1;

        if (decimationStrategy == PlyDecimationStrategy.None)
            decimationFactor = 1;

        long total = 0;
        foreach (var header in headers)
        {
            var sourceCount = header.VertexCount;
            if (decimationStrategy == PlyDecimationStrategy.None || decimationFactor <= 1)
            {
                total += sourceCount;
            }
            else if (decimationStrategy == PlyDecimationStrategy.Stride)
            {
                total += (sourceCount + decimationFactor - 1L) / decimationFactor;
            }
            else
            {
                var estimated = (long)Math.Ceiling(sourceCount / (double)decimationFactor * 1.1);
                total += Math.Min(sourceCount, Math.Max(1, estimated));
            }
        }

        if (total > int.MaxValue)
            throw new InvalidOperationException($"Composed point cloud would exceed maximum vertex count: {total}.");

        return checked((int)total);
    }

    private static void EnsureComposedCapacity(
        Dictionary<string, float[]> composed,
        string[] fieldOrder,
        int requiredLength)
    {
        foreach (var fieldName in fieldOrder)
        {
            if (!composed.TryGetValue(fieldName, out var current))
            {
                composed[fieldName] = new float[requiredLength];
                continue;
            }

            if (current.Length >= requiredLength)
                continue;

            var newLength = Math.Max(requiredLength, Math.Max(1024, current.Length * 2));
            Array.Resize(ref current, newLength);
            composed[fieldName] = current;
        }
    }

    private static void TrimComposedArrays(Dictionary<string, float[]> composed, int finalLength)
    {
        foreach (var fieldName in composed.Keys.ToArray())
        {
            var array = composed[fieldName];
            if (array.Length == finalLength)
                continue;

            Array.Resize(ref array, finalLength);
            composed[fieldName] = array;
        }
    }

    private static void ValidateLoadedFields(
        string file,
        Dictionary<string, float[]> arrays,
        string[] sourceFieldOrder)
    {
        if (arrays.Count == 0)
            throw new InvalidOperationException($"PLY file '{file}' loaded no vertex fields.");

        var expectedLength = -1;
        foreach (var fieldName in sourceFieldOrder)
        {
            if (!arrays.TryGetValue(fieldName, out var array))
                throw new InvalidOperationException($"PLY file '{file}' is missing field '{fieldName}'.");

            if (expectedLength < 0)
            {
                expectedLength = array.Length;
            }
            else if (array.Length != expectedLength)
            {
                throw new InvalidOperationException(
                    $"PLY file '{file}' has inconsistent field lengths. Field '{fieldName}' has " +
                    $"{array.Length} values, expected {expectedLength}.");
            }
        }
    }

    private static string[] EnumeratePlyFiles(string folderPath, string searchPattern, bool recursive)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Folder not found: '{folderPath}'.");

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory
            .EnumerateFiles(folderPath, searchPattern, option)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ValidateCompatibleHeaders(PlyHeaderInfo[] headers)
    {
        if (headers.Length == 0)
            throw new InvalidOperationException("No PLY headers to validate.");

        var first = headers[0];
        for (var i = 1; i < headers.Length; i++)
        {
            var current = headers[i];
            if (!first.FieldOrder.SequenceEqual(current.FieldOrder, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"PLY schema mismatch between '{first.FilePath}' and '{current.FilePath}': field order differs.");
            }

            for (var f = 0; f < first.FieldOrder.Length; f++)
            {
                if (!string.Equals(first.FieldTypes[f], current.FieldTypes[f], StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"PLY schema mismatch between '{first.FilePath}' and '{current.FilePath}': " +
                        $"field '{first.FieldOrder[f]}' has type '{first.FieldTypes[f]}' vs '{current.FieldTypes[f]}'.");
                }
            }
        }
    }

    private static PlyHeaderInfo ReadPlyHeader(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, false);
        var headerBuilder = new StringBuilder(8192);
        var buffer = new byte[4096];
        var foundEndHeader = false;

        while (!foundEndHeader)
        {
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0)
                break;

            for (var i = 0; i < bytesRead; i++)
            {
                var c = (char)buffer[i];
                if (c == '\r')
                    continue;

                headerBuilder.Append(c);
                if (c != '\n')
                    continue;

                if (headerBuilder.ToString().Contains("end_header"))
                {
                    foundEndHeader = true;
                    break;
                }
            }
        }

        if (!foundEndHeader)
            throw new InvalidOperationException($"PLY file '{filePath}' has no end_header marker.");

        var lines = headerBuilder.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var fieldNames = new List<string>();
        var fieldTypes = new List<string>();
        var vertexCount = 0;
        var inVertexElement = false;
        var formatLineFound = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
                continue;

            if (trimmed.StartsWith("format ", StringComparison.Ordinal))
            {
                formatLineFound = true;
                if (!trimmed.StartsWith("format binary_little_endian ", StringComparison.Ordinal))
                    throw new NotSupportedException(
                        $"PLY file '{filePath}' uses unsupported format '{trimmed}'. Only binary_little_endian is supported.");
            }
            else if (trimmed.StartsWith("element ", StringComparison.Ordinal))
            {
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                inVertexElement = parts.Length >= 3 && parts[1] == "vertex";
                if (inVertexElement)
                    vertexCount = int.Parse(parts[2]);
            }
            else if (trimmed.StartsWith("property ", StringComparison.Ordinal) && inVertexElement)
            {
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    throw new InvalidOperationException($"Invalid PLY property line in '{filePath}': '{trimmed}'.");

                if (parts[1] == "list")
                    throw new NotSupportedException(
                        $"PLY file '{filePath}' has a list property in the vertex element, which is not supported.");

                fieldTypes.Add(parts[1]);
                fieldNames.Add(parts[2]);
            }
            else if (trimmed.StartsWith("end_header", StringComparison.Ordinal))
            {
                break;
            }
        }

        if (!formatLineFound)
            throw new InvalidOperationException($"PLY file '{filePath}' has no format line.");

        if (vertexCount <= 0 || fieldNames.Count == 0)
            throw new InvalidOperationException($"PLY file '{filePath}' has no vertex data.");

        return new PlyHeaderInfo(filePath, vertexCount, fieldNames.ToArray(), fieldTypes.ToArray());
    }

    private static (float[] interleaved, int vertexCount, int fieldsPerVertex, string[] fieldOrder)
        ConvertSoAToInterleaved(Dictionary<string, float[]> soaArrays, string[] fieldOrder)
    {
        if (soaArrays.Count == 0 || fieldOrder.Length == 0)
            return (Array.Empty<float>(), 0, 0, Array.Empty<string>());

        var fieldsPerVertex = fieldOrder.Length;
        var fieldArrays = new float[fieldsPerVertex][];
        for (var f = 0; f < fieldsPerVertex; f++)
        {
            if (!soaArrays.TryGetValue(fieldOrder[f], out var arr))
                throw new InvalidOperationException($"Field '{fieldOrder[f]}' not found in composed SoA arrays.");
            fieldArrays[f] = arr;
        }

        var vertexCount = fieldArrays[0].Length;
        for (var f = 1; f < fieldsPerVertex; f++)
        {
            if (fieldArrays[f].Length != vertexCount)
                throw new InvalidOperationException(
                    $"Field '{fieldOrder[f]}' has {fieldArrays[f].Length} elements, expected {vertexCount}.");
        }

        var totalFloats = (long)vertexCount * fieldsPerVertex;
        if (totalFloats > int.MaxValue)
            throw new InvalidOperationException($"Interleaved array would exceed maximum size: {totalFloats} floats.");

        var interleaved = new float[checked((int)totalFloats)];
        const int parallelThreshold = 100_000;

        if (vertexCount >= parallelThreshold)
            ConvertParallel(fieldArrays, interleaved, vertexCount, fieldsPerVertex);
        else
            ConvertSequential(fieldArrays, interleaved, vertexCount, fieldsPerVertex);

        return (interleaved, vertexCount, fieldsPerVertex, fieldOrder);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConvertSequential(float[][] fieldArrays, float[] interleaved, int vertexCount,
        int fieldsPerVertex)
    {
        var writeIdx = 0;
        for (var v = 0; v < vertexCount; v++)
        {
            for (var f = 0; f < fieldsPerVertex; f++)
                interleaved[writeIdx++] = fieldArrays[f][v];
        }
    }

    private static void ConvertParallel(float[][] fieldArrays, float[] interleaved, int vertexCount,
        int fieldsPerVertex)
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
                    interleaved[writeIdx++] = fieldArrays[f][v];
            }
        });
    }

    public void ReleaseCpuData()
    {
        Result = Array.Empty<float>();
        VertexCount = 0;
        FieldsPerVertex = 0;
        FieldOrder = Array.Empty<string>();
    }

    private static void Log(bool debug, string message)
    {
        if (debug) Console.WriteLine(message);
    }

    private sealed class PlyHeaderInfo
    {
        public PlyHeaderInfo(string filePath, int vertexCount, string[] fieldOrder, string[] fieldTypes)
        {
            FilePath = filePath;
            VertexCount = vertexCount;
            FieldOrder = fieldOrder;
            FieldTypes = fieldTypes;
        }

        public string FilePath { get; }
        public int VertexCount { get; }
        public string[] FieldOrder { get; }
        public string[] FieldTypes { get; }
    }
}
