using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable CS1591

namespace Fuse.IO.Ply;

public enum PlyDecimationStrategy
{
    None,

    /// <summary>
    /// Deterministic. Takes every Nth point.
    /// Fast, but might create moire patterns on structured scanner data.
    /// </summary>
    Stride,

    /// <summary>
    /// Probabilistic (Statistically Random). 
    /// Uses a fast integer hash to pick points.
    /// Removes aliasing artifacts on ordered point clouds.
    /// </summary>
    Random
}

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
    public static Dictionary<string, float[]> ReadBinaryPly(
        string filePath,
        PlyDecimationStrategy decimationStrategy = PlyDecimationStrategy.None,
        int decimationFactor = 1)
    {
        var progressInfo = new ProgressInfo();
        // Blocking wait that avoids AggregateException wrapper
        LoadInBackgroundAsync(filePath, progressInfo, decimationStrategy, decimationFactor).GetAwaiter().GetResult();

        if (progressInfo.Error != null)
            throw progressInfo.Error;

        return progressInfo.Result;
    }

    // Optimized background loading method
    public static async Task LoadInBackgroundAsync(
        string filePath,
        ProgressInfo progressInfo,
        PlyDecimationStrategy decimationStrategy,
        int decimationFactor)
    {
        var startTime = DateTime.UtcNow;
        var fileInfo = new FileInfo(filePath);
        var totalFileSize = fileInfo.Length;

        // Validation
        var originalFactor = decimationFactor;
        if (decimationFactor < 1) decimationFactor = 1;
        if (decimationStrategy == PlyDecimationStrategy.None) decimationFactor = 1;
        
        Console.WriteLine($"[FastPlyReader] Input: Strategy={decimationStrategy}, Factor={originalFactor} -> ValidatedFactor={decimationFactor}");

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

            Console.WriteLine($"[FastPlyReader] Header parsed: Properties={properties.Count}, VertexCount={vertexCount:N0}, VertexSize={vertexSize} bytes, HeaderSize={headerSize}");
            Console.WriteLine($"[FastPlyReader] Decimation: Strategy={decimationStrategy}, Factor={decimationFactor}");

            // Estimate target size based on decimation strategy to save memory
            int estimatedTargetCount = vertexCount;
            if (decimationStrategy != PlyDecimationStrategy.None && decimationFactor > 1)
            {
                estimatedTargetCount = vertexCount / decimationFactor;
                // Add a small safety buffer for probabilistic variance if using Random
                if (decimationStrategy == PlyDecimationStrategy.Random)
                {
                    estimatedTargetCount = (int)(estimatedTargetCount * 1.1f);
                }
                estimatedTargetCount = Math.Min(estimatedTargetCount, vertexCount);
            }
            estimatedTargetCount = Math.Max(1, estimatedTargetCount);
            
            Console.WriteLine($"[FastPlyReader] Estimated target count: {estimatedTargetCount:N0} (ratio: {(double)estimatedTargetCount / vertexCount:P2})");

            // Stage 2: Pre-allocate arrays with better memory layout
            progressInfo.Stage = 1;
            progressInfo.StageName = "Allocating memory";
            progressInfo.ProgressPercentage = 5;
            progressInfo.TotalVertices = vertexCount;
            progressInfo.Elapsed = DateTime.UtcNow - startTime;

            // PERFORMANCE: Use Pinned Object Heap (POH)
            // This allows us to get a stable pointer once and use it everywhere without pinning/unpinning overhead.
            var tempArrays = new float[properties.Count][];
            var rawPropertyPointers = new IntPtr[properties.Count]; // Cache pointers for workers

            var allocationTasks = new Task[properties.Count];
            for (var i = 0; i < properties.Count; i++)
            {
                var index = i;
                allocationTasks[i] = Task.Run(() =>
                {
                    // Allocate on POH (Pinned Object Heap) - radical optimization for access speed
                    var pinnedArray = GC.AllocateArray<float>(estimatedTargetCount, pinned: true);
                    tempArrays[index] = pinnedArray;

                    unsafe
                    {
                        // Get the pointer once. Since it's POH, it never moves.
                        fixed (float* ptr = pinnedArray)
                        {
                            rawPropertyPointers[index] = (IntPtr)ptr;
                        }
                    }
                });
            }

            await Task.WhenAll(allocationTasks);

            var scalarFields = new Dictionary<string, float[]>(properties.Count);
            var fieldOrder = new string[properties.Count];
            for (int i = 0; i < properties.Count; i++)
            {
                scalarFields[properties[i].Name] = tempArrays[i];
                fieldOrder[i] = properties[i].Name;
            }
            progressInfo.FieldOrder = fieldOrder;

            // Context for atomic writing across threads
            var context = new LoadContext { GlobalWriteIndex = 0 };

            // Stage 3: Parallel binary data reading
            progressInfo.Stage = 1;
            progressInfo.StageName = "Loading vertex data";
            progressInfo.ProgressPercentage = 10;
            progressInfo.TotalVertices = vertexCount;
            progressInfo.TotalBytes = totalFileSize;
            progressInfo.BytesProcessed = headerSize;
            progressInfo.Elapsed = DateTime.UtcNow - startTime;

            await ReadBinaryDataParallelAsync(fileStream, headerSize, optimizedProperties, rawPropertyPointers,
                vertexCount, vertexSize, progressInfo, startTime, totalFileSize, bufferSize,
                decimationStrategy, decimationFactor, context);

            // Finalize Memory: Resize arrays if we over-allocated during estimation
            int finalVertexCount = context.GlobalWriteIndex;
            Console.WriteLine($"[FastPlyReader] Parallel read complete: GlobalWriteIndex={finalVertexCount:N0}, EstimatedTarget={estimatedTargetCount:N0}");
            
            if (finalVertexCount == 0)
            {
                Console.WriteLine($"[FastPlyReader] WARNING: No vertices written! Strategy={decimationStrategy}, Factor={decimationFactor}, SourceVertices={vertexCount:N0}");
            }
            
            if (finalVertexCount != estimatedTargetCount)
            {
                Console.WriteLine($"[FastPlyReader] Compacting arrays from {estimatedTargetCount:N0} to {finalVertexCount:N0}");
                progressInfo.StageName = "Compacting memory";
                foreach (var key in scalarFields.Keys.ToList())
                {
                    var originalArray = scalarFields[key];
                    if (originalArray.Length != finalVertexCount)
                    {
                        // Note: Resizing a POH array creates a regular heap array copy.
                        // This is fine as we are done with the raw pointers now.
                        Array.Resize(ref originalArray, finalVertexCount);
                        scalarFields[key] = originalArray;
                    }
                }
            }

            // Completion
            progressInfo.Stage = 1;
            progressInfo.StageName = "Complete";
            progressInfo.ProgressPercentage = 100;
            progressInfo.TotalVertices = vertexCount;
            progressInfo.VerticesProcessed = finalVertexCount;
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

    private class LoadContext
    {
        // TODO: THREAD SAFETY - This field is accessed via Interlocked.Add but is not
        // declared volatile. While Interlocked operations provide atomicity, the field
        // should ideally be volatile to ensure proper visibility across threads.
        // Also, verify that all reads of this field use Interlocked.Read or Volatile.Read.
        public int GlobalWriteIndex;
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
        IntPtr[] rawPropertyPointers, int vertexCount, int vertexSize,
        ProgressInfo progressInfo, DateTime startTime, long totalFileSize, int bufferSize,
        PlyDecimationStrategy strategy, int factor, LoadContext context)
    {
        stream.Position = headerSize;

        // Calculate optimal parallel processing parameters
        var maxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8); // Cap at 8 threads

        // Increase chunk size when decimating to reduce atomic contention overhead
        var chunkMultiplier = factor > 1 ? 4 : 1;
        var verticesPerChunk = Math.Max(4096, (vertexCount / (maxDegreeOfParallelism * 4)) * chunkMultiplier);
        
        // CRITICAL: Ensure chunkBytes doesn't overflow int.MaxValue
        // Max safe chunk size in bytes is ~2GB, so limit vertices accordingly
        const int maxSafeChunkBytes = int.MaxValue - 1024; // Leave some margin
        var maxVerticesPerChunk = maxSafeChunkBytes / vertexSize;
        if (verticesPerChunk > maxVerticesPerChunk)
        {
            Console.WriteLine($"[FastPlyReader] Reducing verticesPerChunk from {verticesPerChunk:N0} to {maxVerticesPerChunk:N0} to prevent overflow");
            verticesPerChunk = maxVerticesPerChunk;
        }

        Console.WriteLine($"[FastPlyReader] ReadBinaryDataParallel: VertexCount={vertexCount:N0}, VertexSize={vertexSize}, VerticesPerChunk={verticesPerChunk:N0}, Parallelism={maxDegreeOfParallelism}");
        Console.WriteLine($"[FastPlyReader] Stream position before read: {stream.Position}, Stream length: {stream.Length}");

        // Pre-compile property readers for each property
        var readers = new PropertyReader[properties.Length];
        for (var i = 0; i < properties.Length; i++) readers[i] = PropertyReaders[properties[i].Type];

        // Progress reporting variables
        var lastProgressUpdate = DateTime.UtcNow;
        const int progressUpdateIntervalMs = 50; // Faster updates for large files
        var processedSourceVertices = 0;
        var chunkCount = 0;

        // Process data in parallel chunks
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = new List<Task>();

        Console.WriteLine($"[FastPlyReader] Starting chunk loop. processedSourceVertices={processedSourceVertices}, vertexCount={vertexCount}");

        while (processedSourceVertices < vertexCount)
        {
            var chunkVertices = Math.Min(verticesPerChunk, vertexCount - processedSourceVertices);
            // Use long to prevent overflow, then safely cast
            var chunkBytesLong = (long)chunkVertices * vertexSize;
            if (chunkBytesLong > int.MaxValue)
            {
                Console.WriteLine($"[FastPlyReader] ERROR: chunkBytes overflow! {chunkVertices:N0} * {vertexSize} = {chunkBytesLong:N0}");
                throw new InvalidOperationException($"Chunk size overflow: {chunkBytesLong} bytes exceeds int.MaxValue");
            }
            var chunkBytes = (int)chunkBytesLong;

            if (chunkCount == 0)
            {
                Console.WriteLine($"[FastPlyReader] First chunk: chunkVertices={chunkVertices:N0}, chunkBytes={chunkBytes:N0}");
            }

            // Read chunk data
            var chunkBuffer = new byte[chunkBytes];
            var bytesRead = await stream.ReadAsync(chunkBuffer, 0, chunkBytes);
            
            if (chunkCount == 0)
            {
                Console.WriteLine($"[FastPlyReader] First chunk read: bytesRead={bytesRead:N0}, expected={chunkBytes:N0}");
            }
            
            if (bytesRead == 0)
            {
                Console.WriteLine($"[FastPlyReader] WARNING: bytesRead=0 at chunk {chunkCount}, breaking loop! Stream position: {stream.Position}");
                break;
            }

            var actualVertices = bytesRead / vertexSize;
            var chunkStartVertex = processedSourceVertices;

            // Process chunk in parallel
            await semaphore.WaitAsync();
            var currentChunk = chunkCount;
            var task = Task.Run(() =>
            {
                try
                {
                    unsafe
                    {
                        ProcessChunkUnsafe(chunkBuffer, properties, rawPropertyPointers, readers,
                            chunkStartVertex, actualVertices, vertexSize, strategy, factor, context, currentChunk);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FastPlyReader] EXCEPTION in chunk {currentChunk}: {ex.GetType().Name}: {ex.Message}");
                    throw; // Re-throw to propagate the error
                }
                finally
                {
                    semaphore.Release();
                }
            });

            tasks.Add(task);
            processedSourceVertices += actualVertices;
            chunkCount++;

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
                var progressPercent = 10 + (double)processedSourceVertices / vertexCount * 90;

                progressInfo.Stage = 1;
                progressInfo.StageName = "Loading vertex data";
                progressInfo.ProgressPercentage = progressPercent;
                progressInfo.VerticesProcessed = processedSourceVertices;
                progressInfo.TotalVertices = vertexCount;
                progressInfo.BytesProcessed = stream.Position;
                progressInfo.TotalBytes = totalFileSize;
                progressInfo.Elapsed = now - startTime;

                lastProgressUpdate = now;
            }
        }

        // Wait for all processing tasks to complete
        Console.WriteLine($"[FastPlyReader] Waiting for {tasks.Count} tasks to complete...");
        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FastPlyReader] EXCEPTION during Task.WhenAll: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
        
        Console.WriteLine($"[FastPlyReader] Chunk processing complete: TotalChunks={chunkCount}, ProcessedSourceVertices={processedSourceVertices:N0}, GlobalWriteIndex={context.GlobalWriteIndex:N0}");
    }

    // Radically optimized chunk processing
    private static unsafe void ProcessChunkUnsafe(byte[] chunkBuffer, PropertyInfo[] properties,
        IntPtr[] rawDstPtrs, PropertyReader[] readers,
        int startVertex, int chunkVertexCount, int vertexSize,
        PlyDecimationStrategy strategy, int factor, LoadContext context, int chunkIndex)
    {
        // 1. Calculate Count & Stride strategy fast path
        int keepCount = 0;
        int[]? indicesArray = null;

        if (strategy == PlyDecimationStrategy.None || factor <= 1)
        {
            keepCount = chunkVertexCount;
            // No indices needed, we copy all
        }
        else if (strategy == PlyDecimationStrategy.Stride)
        {
            // Calculate count mathematically
            int globalOffset = startVertex % factor;
            int firstInChunk = globalOffset == 0 ? 0 : factor - globalOffset;
            if (firstInChunk < chunkVertexCount)
            {
                keepCount = (chunkVertexCount - firstInChunk + factor - 1) / factor;
            }
            
            // Debug logging for first few chunks
            if (chunkIndex < 3)
            {
                Console.WriteLine($"[FastPlyReader] Chunk[{chunkIndex}] Stride: startVertex={startVertex}, chunkVertexCount={chunkVertexCount}, factor={factor}, globalOffset={globalOffset}, firstInChunk={firstInChunk}, keepCount={keepCount}");
            }
            // No indices needed, we will stride the pointer
        }
        else // Random
        {
            // Use ArrayPool to avoid stack overflow risks while being fast
            indicesArray = ArrayPool<int>.Shared.Rent(chunkVertexCount);
            uint threshold = (uint)(uint.MaxValue / factor);
            for (int i = 0; i < chunkVertexCount; i++)
            {
                uint x = (uint)(startVertex + i);
                x = (x ^ 61) ^ (x >> 16);
                x = x + (x << 3);
                x = x ^ (x >> 4);
                x = x * 0x27d4eb2d;
                x = x ^ (x >> 15);
                if (x < threshold) indicesArray[keepCount++] = i;
            }
            
            // Debug logging for first few chunks
            if (chunkIndex < 3)
            {
                Console.WriteLine($"[FastPlyReader] Chunk[{chunkIndex}] Random: startVertex={startVertex}, chunkVertexCount={chunkVertexCount}, factor={factor}, threshold={threshold}, keepCount={keepCount}");
            }
        }

        if (keepCount == 0)
        {
            if (chunkIndex < 3)
            {
                Console.WriteLine($"[FastPlyReader] Chunk[{chunkIndex}] WARNING: keepCount=0, skipping chunk! Strategy={strategy}, Factor={factor}");
            }
            if (indicesArray != null) ArrayPool<int>.Shared.Return(indicesArray);
            return;
        }

        // 2. Atomic Reservation
        int writeStart = Interlocked.Add(ref context.GlobalWriteIndex, keepCount) - keepCount;

        // Bounds Check (should happen rarely if estimation is good)
        // Assume first property is representative of size
        // We can cast the IntPtr back to check bounds, but generally we rely on the large estimation
        // For radical speed, we skip per-vertex bound checks and rely on the allocated buffer being large enough (110% for random).

        bool canUseSIMD = properties.Length == 3 &&
                          properties[0].Type == PropertyType.Float &&
                          properties[1].Type == PropertyType.Float &&
                          properties[2].Type == PropertyType.Float &&
                          Vector.IsHardwareAccelerated;

        // 3. Write Data - fixed blocks must encompass all pointer usage
        if (strategy == PlyDecimationStrategy.Random && indicesArray != null)
        {
            // Random path: pin both chunkBuffer and indicesArray together
            fixed (byte* bufferBase = chunkBuffer)
            fixed (int* indicesPtr = indicesArray)
            {
                if (canUseSIMD)
                {
                    CopyGatherSIMD(bufferBase, rawDstPtrs, properties, indicesPtr, keepCount, writeStart, vertexSize);
                }
                else
                {
                    CopyGatherGeneric(bufferBase, rawDstPtrs, properties, readers, indicesPtr, keepCount, writeStart, vertexSize);
                }
            }
            ArrayPool<int>.Shared.Return(indicesArray);
        }
        else
        {
            // Non-random paths: only need to pin chunkBuffer
            fixed (byte* bufferBase = chunkBuffer)
            {
                if (strategy == PlyDecimationStrategy.Stride)
                {
                    int globalOffset = startVertex % factor;
                    int firstInChunk = globalOffset == 0 ? 0 : factor - globalOffset;

                    // Optimized Stride Path: No Indices Array
                    if (canUseSIMD)
                    {
                        CopyStrideSIMD(bufferBase, rawDstPtrs, properties, firstInChunk, keepCount, writeStart, vertexSize, factor);
                    }
                    else
                    {
                        CopyStrideGeneric(bufferBase, rawDstPtrs, properties, readers, firstInChunk, keepCount, writeStart, vertexSize, factor);
                    }
                }
                else // None or factor <= 1
                {
                    // Optimized Continuous Copy Path
                    if (canUseSIMD)
                    {
                        CopyContiguousSIMD(bufferBase, rawDstPtrs, properties, chunkVertexCount, writeStart, vertexSize);
                    }
                    else
                    {
                        CopyContiguousGeneric(bufferBase, rawDstPtrs, properties, readers, chunkVertexCount, writeStart, vertexSize);
                    }
                }
            }
        }
    }

    // --- Optimized Copiers ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void CopyContiguousSIMD(byte* srcBase, IntPtr[] dstPtrs, PropertyInfo[] props, int count, int writeStart, int vertexSize)
    {
        float* dstX = (float*)dstPtrs[0] + writeStart;
        float* dstY = (float*)dstPtrs[1] + writeStart;
        float* dstZ = (float*)dstPtrs[2] + writeStart;
        int ox = props[0].Offset;
        int oy = props[1].Offset;
        int oz = props[2].Offset;

        byte* src = srcBase;

        for (int i = 0; i < count; i++)
        {
            dstX[i] = *(float*)(src + ox);
            dstY[i] = *(float*)(src + oy);
            dstZ[i] = *(float*)(src + oz);
            src += vertexSize;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void CopyStrideSIMD(byte* srcBase, IntPtr[] dstPtrs, PropertyInfo[] props, int startOffset, int count, int writeStart, int vertexSize, int factor)
    {
        float* dstX = (float*)dstPtrs[0] + writeStart;
        float* dstY = (float*)dstPtrs[1] + writeStart;
        float* dstZ = (float*)dstPtrs[2] + writeStart;
        int ox = props[0].Offset;
        int oy = props[1].Offset;
        int oz = props[2].Offset;

        long strideBytes = (long)vertexSize * factor;
        byte* src = srcBase + (startOffset * vertexSize);

        for (int i = 0; i < count; i++)
        {
            dstX[i] = *(float*)(src + ox);
            dstY[i] = *(float*)(src + oy);
            dstZ[i] = *(float*)(src + oz);
            src += strideBytes;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void CopyGatherSIMD(byte* srcBase, IntPtr[] dstPtrs, PropertyInfo[] props, int* indices, int count, int writeStart, int vertexSize)
    {
        float* dstX = (float*)dstPtrs[0] + writeStart;
        float* dstY = (float*)dstPtrs[1] + writeStart;
        float* dstZ = (float*)dstPtrs[2] + writeStart;
        int ox = props[0].Offset;
        int oy = props[1].Offset;
        int oz = props[2].Offset;

        for (int i = 0; i < count; i++)
        {
            byte* src = srcBase + (indices[i] * vertexSize);
            dstX[i] = *(float*)(src + ox);
            dstY[i] = *(float*)(src + oy);
            dstZ[i] = *(float*)(src + oz);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void CopyContiguousGeneric(byte* srcBase, IntPtr[] dstPtrs, PropertyInfo[] props, PropertyReader[] readers, int count, int writeStart, int vertexSize)
    {
        byte* src = srcBase;
        int propCount = props.Length;
        // Resolve pointers to stack for speed
        float** dPtrs = stackalloc float*[propCount];
        for (int p = 0; p < propCount; p++) dPtrs[p] = (float*)dstPtrs[p] + writeStart;

        for (int i = 0; i < count; i++)
        {
            for (int p = 0; p < propCount; p++)
            {
                dPtrs[p][i] = readers[p]((IntPtr)(src + props[p].Offset));
            }
            src += vertexSize;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void CopyStrideGeneric(byte* srcBase, IntPtr[] dstPtrs, PropertyInfo[] props, PropertyReader[] readers, int startOffset, int count, int writeStart, int vertexSize, int factor)
    {
        long strideBytes = (long)vertexSize * factor;
        byte* src = srcBase + (startOffset * vertexSize);
        int propCount = props.Length;

        float** dPtrs = stackalloc float*[propCount];
        for (int p = 0; p < propCount; p++) dPtrs[p] = (float*)dstPtrs[p] + writeStart;

        for (int i = 0; i < count; i++)
        {
            for (int p = 0; p < propCount; p++)
            {
                dPtrs[p][i] = readers[p]((IntPtr)(src + props[p].Offset));
            }
            src += strideBytes;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void CopyGatherGeneric(byte* srcBase, IntPtr[] dstPtrs, PropertyInfo[] props, PropertyReader[] readers, int* indices, int count, int writeStart, int vertexSize)
    {
        int propCount = props.Length;
        float** dPtrs = stackalloc float*[propCount];
        for (int p = 0; p < propCount; p++) dPtrs[p] = (float*)dstPtrs[p] + writeStart;

        for (int i = 0; i < count; i++)
        {
            byte* src = srcBase + (indices[i] * vertexSize);
            for (int p = 0; p < propCount; p++)
            {
                dPtrs[p][i] = readers[p]((IntPtr)(src + props[p].Offset));
            }
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
        /// <summary>
        /// Field names in the order they appear in the PLY header.
        /// This preserves the original property order for AoS interleaving.
        /// </summary>
        public string[] FieldOrder { get; set; } = Array.Empty<string>();
        public Exception? Error { get; set; }
    }
}