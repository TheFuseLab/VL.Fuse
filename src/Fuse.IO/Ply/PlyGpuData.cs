using System.Runtime.InteropServices;
using Stride.Graphics;
using VL.Stride.Graphics;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse.IO.Ply;

#pragma warning disable CS1591

internal sealed class GpuBufferInfo
{
    public static readonly GpuBufferInfo Empty = new(
        string.Empty,
        null,
        default,
        default,
        0,
        0,
        isStructuredBuffer: true,
        isVertexBuffer: false,
        isIndexBuffer: false,
        allowRawViews: false);

    public GpuBufferInfo(
        string name,
        IGraphicsDataProvider? graphicsData,
        BufferDescription description,
        BufferViewDescription viewDescription,
        int elementCount,
        int elementSizeInBytes,
        bool isStructuredBuffer,
        bool isVertexBuffer,
        bool isIndexBuffer,
        bool allowRawViews)
    {
        Name = name;
        _graphicsData = graphicsData;
        Description = description;
        ViewDescription = viewDescription;
        ElementCount = Math.Max(0, elementCount);
        ElementSizeInBytes = Math.Max(0, elementSizeInBytes);
        IsStructuredBuffer = isStructuredBuffer;
        IsVertexBuffer = isVertexBuffer;
        IsIndexBuffer = isIndexBuffer;
        AllowRawViews = allowRawViews;
    }

    private IGraphicsDataProvider? _graphicsData;
    public string Name { get; }
    public IGraphicsDataProvider? GraphicsData => _graphicsData;
    public BufferDescription Description { get; }
    public BufferViewDescription ViewDescription { get; }
    public Buffer? Buffer { get; private set; }
    public string LastBufferStatus { get; private set; } = "Uninitialized";
    public int ElementCount { get; }
    public int ElementSizeInBytes { get; }
    public bool IsStructuredBuffer { get; }
    public bool IsVertexBuffer { get; }
    public bool IsIndexBuffer { get; }
    public bool AllowRawViews { get; }
    public long SizeInBytes => (long)ElementCount * ElementSizeInBytes;

    public Buffer? EnsureBuffer(GraphicsDevice? device)
    {
        if (Buffer != null)
        {
            LastBufferStatus = "AlreadyCreated";
            return Buffer;
        }

        if (Description.SizeInBytes <= 0)
        {
            LastBufferStatus = "Skipped:SizeInBytes<=0";
            return Buffer;
        }

        if (device == null)
        {
            LastBufferStatus = "Failed:GraphicsDevice=null";
            return Buffer;
        }

        var pin = PinnedGraphicsData.None;
        try
        {
            if (_graphicsData != null)
                pin = _graphicsData.Pin();

            var effectiveView = ViewDescription;
            if (effectiveView.Flags == BufferFlags.None)
                effectiveView.Flags = Description.BufferFlags;

            Buffer = BufferExtensions.New(device, Description, effectiveView, pin.Pointer);
            LastBufferStatus = Buffer != null
                ? $"Created:Size={Description.SizeInBytes},Stride={Description.StructureByteStride},Flags={Description.BufferFlags}"
                : "Failed:BufferExtensions.New returned null";

            // Immutable buffers don't need CPU upload data after successful creation.
            if (Buffer != null)
                _graphicsData = null;
        }
        catch (Exception ex)
        {
            Buffer = null;
            LastBufferStatus =
                $"Exception:{ex.GetType().Name}:{ex.Message};Size={Description.SizeInBytes};Stride={Description.StructureByteStride};DescFlags={Description.BufferFlags};ViewFlags={ViewDescription.Flags};ViewFormat={ViewDescription.Format}";
        }
        finally
        {
            pin.Dispose();
        }

        return Buffer;
    }

    /// <summary>Creates the buffer from raw data (a reload streams the fields through one unmanaged buffer).</summary>
    public Buffer? CreateBufferFrom(GraphicsDevice device, IntPtr data)
    {
        if (Buffer != null)
            return Buffer;
        var effectiveView = ViewDescription;
        if (effectiveView.Flags == BufferFlags.None)
            effectiveView.Flags = Description.BufferFlags;
        try
        {
            Buffer = BufferExtensions.New(device, Description, effectiveView, data);
            LastBufferStatus = Buffer != null ? "Created:Stream:Size=" + Description.SizeInBytes : "Failed:BufferExtensions.New returned null";
        }
        catch (Exception ex)
        {
            Buffer = null;
            LastBufferStatus = "Exception:" + ex.GetType().Name + ":" + ex.Message;
        }
        return Buffer;
    }

    public void DisposeBuffer()
    {
        Buffer?.Dispose();
        Buffer = null;
        LastBufferStatus = "Disposed";
    }
}

public sealed class PlyGpuData
{
    public static readonly PlyGpuData Empty = new(
        new Dictionary<string, GpuBufferInfo>(0),
        new Dictionary<string, GpuBufferInfo>(0),
        0,
        Array.Empty<string>());

    internal PlyGpuData(
        Dictionary<string, GpuBufferInfo> plyBuffers,
        Dictionary<string, GpuBufferInfo> octreeBuffers,
        int vertexCount,
        string[] fieldOrder)
    {
        _plyInfos = plyBuffers ?? new Dictionary<string, GpuBufferInfo>(0);
        _octreeInfos = octreeBuffers ?? new Dictionary<string, GpuBufferInfo>(0);
        PlyBuffers = new Dictionary<string, Buffer>(StringComparer.OrdinalIgnoreCase);
        OctreeBuffers = default;
        VertexCount = Math.Max(0, vertexCount);
        FieldOrder = fieldOrder ?? Array.Empty<string>();
    }

    private Dictionary<string, GpuBufferInfo> _plyInfos;
    private Dictionary<string, GpuBufferInfo> _octreeInfos;

    public Dictionary<string, Buffer> PlyBuffers { get; }
    public OctreeGpuBuffers OctreeBuffers { get; private set; }
    public int VertexCount { get; }
    public string[] FieldOrder { get; }
    public int PlyBufferDefinitionCount => _plyInfos.Count;
    public int OctreeBufferDefinitionCount => _octreeInfos.Count;
    public bool AreAllPlyBuffersCreated => _plyInfos.Count > 0 && PlyBuffers.Count == _plyInfos.Count;

    /// <summary>Number of field buffers taken away by <see cref="ReleasePlyBuffers"/> (their keys stay with null).</summary>
    public int ReleasedPlyBufferCount { get; private set; }

    /// <summary>
    /// Disposes the field buffers and keeps their keys in <see cref="PlyBuffers"/> with null values, so a patch that
    /// takes a field by name gets null instead of a missing key (a Fuse BufferIn keeps its declaration on null). The
    /// infos are dropped, so <see cref="EnsureBuffers"/> does not create them again. Returns the bytes released.
    /// </summary>
    internal long ReleasePlyBuffers()
    {
        long bytes = 0;
        foreach (var info in _plyInfos.Values)
        {
            if (info == null) continue;
            if (info.Buffer != null) bytes += info.SizeInBytes;
            info.DisposeBuffer();
        }
        foreach (var key in PlyBuffers.Keys.ToArray())
            PlyBuffers[key] = null!;
        ReleasedPlyBufferCount = _plyInfos.Count;
        _plyInfos = new Dictionary<string, GpuBufferInfo>(0);
        return bytes;
    }

    /// <summary>
    /// Puts field buffers created elsewhere (a reload) in place of released ones. The infos must already hold their
    /// buffers (GpuBufferInfo.EnsureBuffer). Returns true when every field has a buffer again.
    /// </summary>
    internal bool RestorePlyBuffers(Dictionary<string, GpuBufferInfo> infos)
    {
        foreach (var info in _plyInfos.Values)
            info?.DisposeBuffer();
        _plyInfos = infos ?? new Dictionary<string, GpuBufferInfo>(0);
        foreach (var kv in _plyInfos)
            PlyBuffers[kv.Key] = kv.Value?.Buffer!;
        ReleasedPlyBufferCount = 0;
        return _plyInfos.Count > 0 && _plyInfos.Values.All(i => i?.Buffer != null);
    }

    public void EnsureBuffers(GraphicsDevice? device)
    {
        foreach (var kv in _plyInfos)
        {
            var buffer = kv.Value?.EnsureBuffer(device);
            if (buffer != null)
                PlyBuffers[kv.Key] = buffer;
            else
                PlyBuffers.Remove(kv.Key);
        }

        foreach (var kv in _octreeInfos)
        {
            var buffer = kv.Value?.EnsureBuffer(device);
            SetOctreeBuffer(kv.Key, buffer);
        }
    }

    public void DisposeBuffers()
    {
        foreach (var info in _plyInfos.Values)
            info?.DisposeBuffer();
        foreach (var info in _octreeInfos.Values)
            info?.DisposeBuffer();
        PlyBuffers.Clear();
        OctreeBuffers = default;
    }

    public string GetPlyBufferStatus(string key)
    {
        return _plyInfos.TryGetValue(key, out var info) ? info.LastBufferStatus : "Missing";
    }

    public string GetOctreeBufferStatus(string key)
    {
        return _octreeInfos.TryGetValue(key, out var info) ? info.LastBufferStatus : "Missing";
    }

    internal void ReplaceOctreeBufferInfos(Dictionary<string, GpuBufferInfo> octreeBuffers, GraphicsDevice? device)
    {
        foreach (var info in _octreeInfos.Values)
            info?.DisposeBuffer();
        _octreeInfos = octreeBuffers ?? new Dictionary<string, GpuBufferInfo>(0);
        OctreeBuffers = default;
        foreach (var kv in _octreeInfos)
        {
            var buffer = kv.Value?.EnsureBuffer(device);
            SetOctreeBuffer(kv.Key, buffer);
        }
    }

    private void SetOctreeBuffer(string key, Buffer? buffer)
    {
        if (string.Equals(key, "nodes", StringComparison.OrdinalIgnoreCase))
            OctreeBuffers = new OctreeGpuBuffers { Nodes = buffer, Indices = OctreeBuffers.Indices };
        else if (string.Equals(key, "indices", StringComparison.OrdinalIgnoreCase))
            OctreeBuffers = new OctreeGpuBuffers { Nodes = OctreeBuffers.Nodes, Indices = buffer };
    }

    public long GetEstimatedRetainedBytes()
    {
        long bytes = 0;
        foreach (var info in _plyInfos.Values)
        {
            if (info != null)
                bytes += info.SizeInBytes;
        }
        foreach (var info in _octreeInfos.Values)
        {
            if (info != null)
                bytes += info.SizeInBytes;
        }
        return bytes;
    }
}

public struct OctreeGpuBuffers
{
    public Buffer? Nodes;
    public Buffer? Indices;
}

internal static class PlyGpuDataFactory
{
    /// <summary>A field buffer description without data (filled by GpuBufferInfo.CreateBufferFrom).</summary>
    public static GpuBufferInfo CreatePlyFieldBufferInfo(string name, int floatCount)
    {
        var (description, viewDescription) = CreateBufferDescriptions(floatCount, sizeof(float), isStructuredBuffer: true,
            isVertexBuffer: false, isIndexBuffer: false, allowRawViews: false);
        return new GpuBufferInfo(name, null, description, viewDescription, floatCount, sizeof(float),
            isStructuredBuffer: true, isVertexBuffer: false, isIndexBuffer: false, allowRawViews: false);
    }

    public static Dictionary<string, GpuBufferInfo> CreateInterleavedFloatBuffer(
        float[] interleaved,
        string bufferName = "interleaved")
    {
        var buffers = new Dictionary<string, GpuBufferInfo>(StringComparer.OrdinalIgnoreCase);
        var values = interleaved ?? Array.Empty<float>();
        if (values.Length == 0)
            return buffers;

        var provider = CreateProvider(values);
        var (description, viewDescription) = CreateBufferDescriptions(
            values.Length,
            sizeof(float),
            isStructuredBuffer: true,
            isVertexBuffer: false,
            isIndexBuffer: false,
            allowRawViews: false);
        buffers[bufferName] = new GpuBufferInfo(
            bufferName,
            provider,
            description,
            viewDescription,
            values.Length,
            sizeof(float),
            isStructuredBuffer: true,
            isVertexBuffer: false,
            isIndexBuffer: false,
            allowRawViews: false);
        return buffers;
    }

    public static Dictionary<string, GpuBufferInfo> CreatePlyFieldBuffers(
        Dictionary<string, float[]> arrays,
        string[] fieldOrder)
    {
        var buffers = new Dictionary<string, GpuBufferInfo>(StringComparer.OrdinalIgnoreCase);
        if (arrays == null || arrays.Count == 0)
            return buffers;

        var keys = (fieldOrder != null && fieldOrder.Length > 0) ? fieldOrder : arrays.Keys.ToArray();
        foreach (var key in keys)
        {
            if (!arrays.TryGetValue(key, out var values) || values == null || values.Length == 0)
                continue;

            var provider = CreateProvider(values);
            var (description, viewDescription) = CreateBufferDescriptions(
                values.Length,
                sizeof(float),
                isStructuredBuffer: true,
                isVertexBuffer: false,
                isIndexBuffer: false,
                allowRawViews: false);
            buffers[key] = new GpuBufferInfo(
                key,
                provider,
                description,
                viewDescription,
                values.Length,
                sizeof(float),
                isStructuredBuffer: true,
                isVertexBuffer: false,
                isIndexBuffer: false,
                allowRawViews: false);
        }

        return buffers;
    }

    public static Dictionary<string, GpuBufferInfo> CreateOctreeBuffers(
        byte[] nodeBufferData,
        int nodeCount,
        byte[] indexBufferData,
        int indexCount)
    {
        var buffers = new Dictionary<string, GpuBufferInfo>(StringComparer.OrdinalIgnoreCase);

        var nodeStride = Marshal.SizeOf<GPUOctree.GPUOctreeNode>();
        var nodes = nodeBufferData ?? Array.Empty<byte>();
        var nodeProvider = CreateProvider(nodes, nodeStride);
        var (nodeDesc, nodeView) = CreateBufferDescriptions(
            Math.Max(0, nodeCount),
            nodeStride,
            isStructuredBuffer: true,
            isVertexBuffer: false,
            isIndexBuffer: false,
            allowRawViews: false);
        buffers["nodes"] = new GpuBufferInfo(
            "nodes",
            nodeProvider,
            nodeDesc,
            nodeView,
            Math.Max(0, nodeCount),
            nodeStride,
            isStructuredBuffer: true,
            isVertexBuffer: false,
            isIndexBuffer: false,
            allowRawViews: false);

        var indices = indexBufferData ?? Array.Empty<byte>();
        var indexProvider = CreateProvider(indices, sizeof(int));
        var (indexDesc, indexView) = CreateBufferDescriptions(
            Math.Max(0, indexCount),
            sizeof(int),
            isStructuredBuffer: false,
            isVertexBuffer: false,
            isIndexBuffer: true,
            allowRawViews: false);
        buffers["indices"] = new GpuBufferInfo(
            "indices",
            indexProvider,
            indexDesc,
            indexView,
            Math.Max(0, indexCount),
            sizeof(int),
            isStructuredBuffer: false,
            isVertexBuffer: false,
            isIndexBuffer: true,
            allowRawViews: false);

        return buffers;
    }

    private static IGraphicsDataProvider CreateProvider(byte[] data, int elementSizeInBytes)
    {
        var provider = new MemoryDataProvider();
        provider.SetMemoryData<byte>(
            new ReadOnlyMemory<byte>(data),
            0,
            data.Length,
            elementSizeInBytes,
            0,
            0);
        return provider;
    }

    private static IGraphicsDataProvider CreateProvider(float[] data)
    {
        var provider = new MemoryDataProvider();
        var length = data?.Length ?? 0;
        provider.SetMemoryData<float>(
            new ReadOnlyMemory<float>(data ?? Array.Empty<float>()),
            0,
            length * sizeof(float),
            sizeof(float),
            0,
            0);
        return provider;
    }

    private static (BufferDescription description, BufferViewDescription viewDescription) CreateBufferDescriptions(
        int elementCount,
        int elementSizeInBytes,
        bool isStructuredBuffer,
        bool isVertexBuffer,
        bool isIndexBuffer,
        bool allowRawViews)
    {
        var sizeInBytes = Math.Max(0, elementCount) * Math.Max(0, elementSizeInBytes);

        var flags = BufferFlags.ShaderResource;
        if (isVertexBuffer)
            flags |= BufferFlags.VertexBuffer;
        if (isIndexBuffer)
            flags |= BufferFlags.IndexBuffer;
        if (allowRawViews)
            flags |= BufferFlags.RawBuffer;
        if (isStructuredBuffer)
            flags |= BufferFlags.StructuredBuffer;

        var description = new BufferDescription(sizeInBytes, flags, GraphicsResourceUsage.Immutable)
        {
            StructureByteStride = isStructuredBuffer ? elementSizeInBytes : 0
        };

        var viewFlags = BufferFlags.None;
        if (allowRawViews)
            viewFlags |= BufferFlags.RawBuffer;
        else if (isStructuredBuffer)
            viewFlags |= BufferFlags.StructuredBuffer;
        else if (isIndexBuffer)
            viewFlags |= BufferFlags.ShaderResource;

        var viewDescription = new BufferViewDescription
        {
            Flags = viewFlags,
            Format = (allowRawViews || isStructuredBuffer) ? PixelFormat.None : (isIndexBuffer ? PixelFormat.R32_UInt : PixelFormat.None)
        };

        return (description, viewDescription);
    }
}
