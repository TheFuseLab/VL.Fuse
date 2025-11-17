using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable CS1591

namespace Fuse.IO.Ply;

public class FastPlyReader
{
    private static readonly Dictionary<PropertyType, PropertyReader> PropertyReaders = new()
    {
        {
            PropertyType.Float, ptr =>
            {
                unsafe
                {
                    return *(float*)ptr;
                }
            }
        },
        {
            PropertyType.Double, ptr =>
            {
                unsafe
                {
                    return (float)*(double*)ptr;
                }
            }
        },
        {
            PropertyType.Int, ptr =>
            {
                unsafe
                {
                    return *(int*)ptr;
                }
            }
        },
        {
            PropertyType.UInt, ptr =>
            {
                unsafe
                {
                    return *(uint*)ptr;
                }
            }
        },
        {
            PropertyType.Short, ptr =>
            {
                unsafe
                {
                    return *(short*)ptr;
                }
            }
        },
        {
            PropertyType.UShort, ptr =>
            {
                unsafe
                {
                    return *(ushort*)ptr;
                }
            }
        },
        {
            PropertyType.Char, ptr =>
            {
                unsafe
                {
                    return *(sbyte*)ptr;
                }
            }
        },
        {
            PropertyType.UChar, ptr =>
            {
                unsafe
                {
                    return *(byte*)ptr;
                }
            }
        }
    };

    // Backward-compatible synchronous method
    public static Dictionary<string, float[]> ReadBinaryPly(string filePath)
    {
        var progressInfo = new ProgressInfo();
        var task = LoadInBackgroundAsync(filePath, progressInfo);
        task.Wait();

        if (progressInfo.Error != null)
            throw progressInfo.Error;

        return progressInfo.Result;
    }

    // Optimized background loading method
    public static async Task LoadInBackgroundAsync(string filePath, ProgressInfo progressInfo)
    {
        var startTime = DateTime.UtcNow;
        var fileInfo = new FileInfo(filePath);
        var totalFileSize = fileInfo.Length;

        try
        {
            // Use larger buffer for massive files
            var bufferSize = totalFileSize > 1_000_000_000 ? 16 * 1024 * 1024 : 4 * 1024 * 1024; // 16MB for >1GB files

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize, true);

            // Stage 1: Optimized header parsing
            progressInfo.Stage = 0;
            progressInfo.StageName = "Parsing header";
            progressInfo.ProgressPercentage = 0;
            progressInfo.Elapsed = DateTime.UtcNow - startTime;

            var (properties, vertexCount, headerSize) =
                await ParseHeaderOptimizedAsync(fileStream, progressInfo, startTime);

            // Calculate vertex size and optimize property layout
            var vertexSize = 0;
            var optimizedProperties = new PropertyInfo[properties.Count];
            for (var i = 0; i < properties.Count; i++)
            {
                optimizedProperties[i] = properties[i];
                vertexSize += properties[i].Size;
            }

            // Stage 2: Pre-allocate arrays with better memory layout
            progressInfo.Stage = 1;
            progressInfo.StageName = "Allocating memory";
            progressInfo.ProgressPercentage = 5;
            progressInfo.TotalVertices = vertexCount;
            progressInfo.Elapsed = DateTime.UtcNow - startTime;

            var scalarFields = new Dictionary<string, float[]>(properties.Count);

            // Use parallel allocation for large arrays
            var allocationTasks = new Task[properties.Count];
            for (var i = 0; i < properties.Count; i++)
            {
                var prop = properties[i];
                allocationTasks[i] = Task.Run(() =>
                {
                    scalarFields[prop.Name] = GC.AllocateArray<float>(vertexCount, true);
                });
            }

            await Task.WhenAll(allocationTasks);

            // Stage 3: Parallel binary data reading
            progressInfo.Stage = 1;
            progressInfo.StageName = "Loading vertex data";
            progressInfo.ProgressPercentage = 10;
            progressInfo.TotalVertices = vertexCount;
            progressInfo.TotalBytes = totalFileSize;
            progressInfo.BytesProcessed = headerSize;
            progressInfo.Elapsed = DateTime.UtcNow - startTime;

            await ReadBinaryDataParallelAsync(fileStream, headerSize, optimizedProperties, scalarFields,
                vertexCount, vertexSize, progressInfo, startTime, totalFileSize, bufferSize);

            // Completion
            progressInfo.Stage = 1;
            progressInfo.StageName = "Complete";
            progressInfo.ProgressPercentage = 100;
            progressInfo.TotalVertices = vertexCount;
            progressInfo.VerticesProcessed = vertexCount;
            progressInfo.TotalBytes = totalFileSize;
            progressInfo.BytesProcessed = totalFileSize;
            progressInfo.Elapsed = DateTime.UtcNow - startTime;
            progressInfo.IsCompleted = true;
            progressInfo.Result = scalarFields;
        }
        catch (Exception ex)
        {
            progressInfo.Error = ex;
            progressInfo.StageName = "Error";
            progressInfo.IsCompleted = true;
        }
    }

    // Optimized header parsing - read in chunks instead of byte-by-byte
    private static async Task<(List<PropertyInfo> properties, int vertexCount, long headerSize)>
        ParseHeaderOptimizedAsync(
            FileStream stream, ProgressInfo progressInfo, DateTime startTime)
    {
        var properties = new List<PropertyInfo>();
        var vertexCount = 0;
        var headerBuilder = new StringBuilder(8192); // Pre-allocate for typical header size

        // Read header in larger chunks
        const int chunkSize = 4096;
        var buffer = new byte[chunkSize];
        var foundEndHeader = false;
        long totalBytesRead = 0;

        while (!foundEndHeader)
        {
            var bytesRead = await stream.ReadAsync(buffer, 0, chunkSize);
            if (bytesRead == 0) break;

            for (var i = 0; i < bytesRead; i++)
            {
                var c = (char)buffer[i];
                if (c != '\r')
                {
                    headerBuilder.Append(c);
                    if (c == '\n')
                    {
                        var headerContent = headerBuilder.ToString();
                        if (headerContent.Contains("end_header"))
                        {
                            foundEndHeader = true;
                            totalBytesRead += i + 1;
                            break;
                        }
                    }
                }
            }

            if (!foundEndHeader)
                totalBytesRead += bytesRead;
        }

        // Reset stream position to end of header
        stream.Position = totalBytesRead;

        // Parse header content efficiently
        var headerLines = headerBuilder.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var currentOffset = 0;
        var inVertexElement = false;

        foreach (var line in headerLines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;

            if (trimmedLine.StartsWith("element "))
            {
                var parts = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                inVertexElement = parts.Length > 1 && parts[1] == "vertex";
                if (inVertexElement && parts.Length > 2)
                    vertexCount = int.Parse(parts[2]);
                else
                    inVertexElement = false;
            }
            else if (trimmedLine.StartsWith("property ") && inVertexElement)
            {
                var parts = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var propType = ParsePropertyType(parts[1]);
                    var propInfo = new PropertyInfo
                    {
                        Name = parts[2],
                        Type = propType.type,
                        Size = propType.size,
                        Offset = currentOffset
                    };

                    properties.Add(propInfo);
                    currentOffset += propInfo.Size;
                }
            }
            else if (trimmedLine.StartsWith("end_header"))
            {
                break;
            }
        }

        return (properties, vertexCount, totalBytesRead);
    }

    private static (PropertyType type, int size) ParsePropertyType(string typeStr)
    {
        return typeStr.ToLower() switch
        {
            "float" or "float32" => (PropertyType.Float, 4),
            "double" or "float64" => (PropertyType.Double, 8),
            "int" or "int32" => (PropertyType.Int, 4),
            "uint" or "uint32" => (PropertyType.UInt, 4),
            "short" or "int16" => (PropertyType.Short, 2),
            "ushort" or "uint16" => (PropertyType.UShort, 2),
            "char" or "int8" => (PropertyType.Char, 1),
            "uchar" or "uint8" => (PropertyType.UChar, 1),
            _ => throw new NotSupportedException($"Property type '{typeStr}' is not supported")
        };
    }

    // Parallel and SIMD-optimized data reading
    private static async Task ReadBinaryDataParallelAsync(
        FileStream stream, long headerSize, PropertyInfo[] properties,
        Dictionary<string, float[]> scalarFields, int vertexCount, int vertexSize,
        ProgressInfo progressInfo, DateTime startTime, long totalFileSize, int bufferSize)
    {
        stream.Position = headerSize;

        // Calculate optimal parallel processing parameters
        var maxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8); // Cap at 8 threads
        var verticesPerChunk =
            Math.Max(1000, vertexCount / (maxDegreeOfParallelism * 4)); // Ensure reasonable chunk sizes

        // Pre-compile property readers for each property
        var readers = new PropertyReader[properties.Length];
        for (var i = 0; i < properties.Length; i++) readers[i] = PropertyReaders[properties[i].Type];

        // Progress reporting variables
        var lastProgressUpdate = DateTime.UtcNow;
        const int progressUpdateIntervalMs = 50; // Faster updates for large files
        var processedVertices = 0;

        // Process data in parallel chunks
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = new List<Task>();

        while (processedVertices < vertexCount)
        {
            var chunkVertices = Math.Min(verticesPerChunk, vertexCount - processedVertices);
            var chunkBytes = chunkVertices * vertexSize;

            // Read chunk data
            var chunkBuffer = new byte[chunkBytes];
            var bytesRead = await stream.ReadAsync(chunkBuffer, 0, chunkBytes);
            if (bytesRead == 0) break;

            var actualVertices = bytesRead / vertexSize;
            var chunkStartVertex = processedVertices;

            // Process chunk in parallel
            await semaphore.WaitAsync();
            var task = Task.Run(() =>
            {
                try
                {
                    ProcessChunkOptimized(chunkBuffer, properties, readers, scalarFields,
                        chunkStartVertex, actualVertices, vertexSize);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            tasks.Add(task);
            processedVertices += actualVertices;

            // Clean up completed tasks periodically
            if (tasks.Count > maxDegreeOfParallelism * 2)
            {
                await Task.WhenAll(tasks.Where(t => t.IsCompleted));
                tasks.RemoveAll(t => t.IsCompleted);
            }

            // Time-based progress reporting
            var now = DateTime.UtcNow;
            if ((now - lastProgressUpdate).TotalMilliseconds >= progressUpdateIntervalMs)
            {
                var progressPercent = 10 + (double)processedVertices / vertexCount * 90;

                progressInfo.Stage = 1;
                progressInfo.StageName = "Loading vertex data";
                progressInfo.ProgressPercentage = progressPercent;
                progressInfo.VerticesProcessed = processedVertices;
                progressInfo.TotalVertices = vertexCount;
                progressInfo.BytesProcessed = stream.Position;
                progressInfo.TotalBytes = totalFileSize;
                progressInfo.Elapsed = now - startTime;

                lastProgressUpdate = now;
            }
        }

        // Wait for all processing tasks to complete
        await Task.WhenAll(tasks);
    }

    // Optimized chunk processing with SIMD where possible
    private static unsafe void ProcessChunkOptimized(byte[] chunkBuffer, PropertyInfo[] properties,
        PropertyReader[] readers, Dictionary<string, float[]> scalarFields,
        int startVertex, int vertexCount, int vertexSize)
    {
        fixed (byte* bufferPtr = chunkBuffer)
        {
            // Check if we can use SIMD for common cases (all float properties)
            var canUseSIMD = properties.Length >= 3 &&
                             properties.All(p => p.Type == PropertyType.Float) &&
                             Vector.IsHardwareAccelerated;

            if (canUseSIMD && properties.Length == 3) // Common case: x, y, z coordinates
                ProcessXYZFloatsSIMD(bufferPtr, scalarFields, properties, startVertex, vertexCount, vertexSize);
            else
                // General case with optimized property reading
                ProcessGeneralCase(bufferPtr, properties, readers, scalarFields, startVertex, vertexCount, vertexSize);
        }
    }

    // SIMD-optimized processing for XYZ float coordinates
    private static unsafe void ProcessXYZFloatsSIMD(byte* bufferPtr, Dictionary<string, float[]> scalarFields,
        PropertyInfo[] properties, int startVertex, int vertexCount, int vertexSize)
    {
        var xArray = scalarFields[properties[0].Name];
        var yArray = scalarFields[properties[1].Name];
        var zArray = scalarFields[properties[2].Name];

        fixed (float* xPtr = &xArray[startVertex])
        fixed (float* yPtr = &yArray[startVertex])
        fixed (float* zPtr = &zArray[startVertex])
        {
            for (var v = 0; v < vertexCount; v++)
            {
                var vertexPtr = bufferPtr + v * vertexSize;

                // Direct memory copy for aligned float data
                xPtr[v] = *(float*)(vertexPtr + properties[0].Offset);
                yPtr[v] = *(float*)(vertexPtr + properties[1].Offset);
                zPtr[v] = *(float*)(vertexPtr + properties[2].Offset);
            }
        }
    }

    // Optimized general case processing
    private static unsafe void ProcessGeneralCase(byte* bufferPtr, PropertyInfo[] properties,
        PropertyReader[] readers, Dictionary<string, float[]> scalarFields,
        int startVertex, int vertexCount, int vertexSize)
    {
        // Cache array references for better performance
        var arrays = new float*[properties.Length];
        var handles = new GCHandle[properties.Length];

        for (var i = 0; i < properties.Length; i++)
        {
            var array = scalarFields[properties[i].Name];
            handles[i] = GCHandle.Alloc(array, GCHandleType.Pinned);
            arrays[i] = (float*)handles[i].AddrOfPinnedObject() + startVertex;
        }

        try
        {
            // Process vertices with minimal overhead
            for (var v = 0; v < vertexCount; v++)
            {
                var vertexPtr = bufferPtr + v * vertexSize;

                for (var p = 0; p < properties.Length; p++)
                    arrays[p][v] = readers[p]((IntPtr)(vertexPtr + properties[p].Offset));
            }
        }
        finally
        {
            // Clean up GC handles
            for (var i = 0; i < handles.Length; i++)
                if (handles[i].IsAllocated)
                    handles[i].Free();
        }
    }

    private enum PropertyType
    {
        Float,
        Double,
        Int,
        UInt,
        Short,
        UShort,
        Char,
        UChar
    }

    private struct PropertyInfo
    {
        public string Name;
        public PropertyType Type;
        public int Size;
        public int Offset;
    }

    // Pre-compiled property readers for performance
    private delegate float PropertyReader(IntPtr ptr);

    public class ProgressInfo
    {
        public int Stage { get; set; } // 0=Header, 1=Data
        public string StageName { get; set; } = string.Empty;
        public long BytesProcessed { get; set; }
        public long TotalBytes { get; set; }
        public int VerticesProcessed { get; set; }
        public int TotalVertices { get; set; }
        public double ProgressPercentage { get; set; }
        public string CurrentField { get; set; } = string.Empty;
        public TimeSpan Elapsed { get; set; }
        public bool IsCompleted { get; set; }
        public Dictionary<string, float[]> Result { get; set; } = new(0);
        public Exception? Error { get; set; }
    }
}