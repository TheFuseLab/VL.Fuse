using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vector3 = Stride.Core.Mathematics.Vector3;

namespace Fuse.IO.Ply;

/// <summary>
/// Helper class for computing precomputed LODs (Level of Detail) for octree leaf nodes.
/// This reorders point indices within each leaf to support progressive rendering.
/// </summary>
public static class OctreeLodHelper
{
    /// <summary>
    /// Applies precomputed LOD ordering to all leaf nodes in the octree.
    /// Points are reordered within each leaf for coarse-to-fine progressive access.
    /// </summary>
    /// <param name="nodeBufferData">The octree node buffer (GPUOctreeNode array as bytes)</param>
    /// <param name="indexBufferData">The point index buffer (int array as bytes)</param>
    /// <param name="nodeCount">Number of nodes in the buffer</param>
    /// <param name="indexCount">Number of indices in the buffer</param>
    /// <param name="plyData">Point cloud data with x, y, z position arrays</param>
    /// <param name="leafTargetCellsOnLongestAxis">Target cell count for LOD grid (24-48 recommended)</param>
    /// <param name="debug">Enable debug logging</param>
    /// <returns>True if LODs were applied successfully</returns>
    public static bool TryApplyPrecomputedLeafLODs(
        byte[] nodeBufferData,
        byte[] indexBufferData,
        int nodeCount,
        int indexCount,
        Dictionary<string, float[]> plyData,
        int leafTargetCellsOnLongestAxis,
        bool debug = false)
    {
        if (nodeBufferData == null || indexBufferData == null || nodeCount <= 0 || indexCount <= 0)
            return false;

        if (!TryGetPositions(plyData, out var posX, out var posY, out var posZ) ||
            posX.Length != posY.Length || posX.Length != posZ.Length)
            return false;

        var posVec = new Vector3[posX.Length];
        for (var i = 0; i < posVec.Length; i++)
            posVec[i] = new Vector3(posX[i], posY[i], posZ[i]);

        var targetCells = Math.Max(4, leafTargetCellsOnLongestAxis);
        var leafCount = 0;

        Parallel.For(0, nodeCount, i =>
        {
            // Create spans locally to avoid captures
            var nodeSpan = MemoryMarshal.Cast<byte, CpuNode>(nodeBufferData.AsSpan());
            var indicesSpan = MemoryMarshal.Cast<byte, int>(indexBufferData.AsSpan());

            var n = nodeSpan[i];
            var isLeaf = n.ChildStartIndex == 0xFFFFFFFFu || unchecked((int)n.ChildStartIndex) == -1;
            if (!isLeaf) return;

            Interlocked.Increment(ref leafCount);

            var start = (int)n.IndexStartIndex;
            var count = (int)n.IndexCount;
            if ((uint)start >= (uint)indicesSpan.Length || count <= 1 || start + count > indicesSpan.Length)
                return;

            var b = new Bounds { Min = n.BoundsMin, Max = n.BoundsMax };
            var size = b.Size;
            var longest = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
            var baseCell = longest > 0 ? longest / targetCells : 1e-4f;

            var slice = indicesSpan.Slice(start, count);
            ProgressiveOrderLeaf(slice, posVec, in b, baseCell);
        });

        if (debug)
        {
            int nonEmpty = 0, empty = 0;
            for (var i = 0; i < nodeCount; i++)
            {
                var n = MemoryMarshal.Cast<byte, CpuNode>(nodeBufferData.AsSpan())[i];
                if (n.IndexCount > 0) nonEmpty++;
                else empty++;
            }

            Console.WriteLine($"[OctreeLodHelper] Nodes: {nodeCount}, leaves: {leafCount}, nonEmpty: {nonEmpty}, totalIndices: {indexCount}");

            var allIndicesSpan = MemoryMarshal.Cast<byte, int>(indexBufferData.AsSpan());
            var previewCount = Math.Min(16, indexCount);
            if (previewCount > 0)
            {
                var preview = allIndicesSpan.Slice(0, previewCount).ToArray();
                Console.WriteLine($"[OctreeLodHelper] First indices: {string.Join(",", preview)}");
            }
        }

        return true;
    }

    /// <summary>
    /// Reorders indices within a single leaf node for progressive LOD rendering.
    /// Uses multi-level grid cells to select coarse points first, then finer ones.
    /// </summary>
    private static void ProgressiveOrderLeaf(
        Span<int> indices,
        ReadOnlySpan<Vector3> positions,
        in Bounds b,
        float baseCellSize)
    {
        if (indices.Length <= 1) return;

        var used = ArrayPool<byte>.Shared.Rent(indices.Length);
        try
        {
            Array.Clear(used, 0, indices.Length);

            // Deterministic shuffle based on bounds to ensure consistency
            ShuffleDeterministic(indices, BoundsKeySeed.FromBounds(in b));

            var size = b.Size;
            var longest = MathF.Max(size.X, MathF.Max(size.Y, size.Z));
            var baseCell = MathF.Max(1e-4f, baseCellSize);
            var levels = Math.Max(1, (int)MathF.Ceiling(MathF.Log2(MathF.Max(1e-6f, longest / baseCell))));

            var w = 0;

            // Process from coarse to fine levels
            for (var level = 0; level < levels; level++)
            {
                var cell = baseCell * (1 << level);
                var seen = new Dictionary<long, byte>(Math.Min(indices.Length, 4096));

                for (var i = 0; i < indices.Length; i++)
                {
                    if (used[i] != 0) continue;

                    var id = indices[i];
                    var p = positions[id];

                    var gx = (int)MathF.Floor((p.X - b.Min.X) / cell);
                    var gy = (int)MathF.Floor((p.Y - b.Min.Y) / cell);
                    var gz = (int)MathF.Floor((p.Z - b.Min.Z) / cell);
                    var key = CellKey(gx, gy, gz);

                    if (!seen.ContainsKey(key))
                    {
                        seen[key] = 1;
                        used[i] = 1;
                        indices[w++] = id;
                        if (w == indices.Length) break;
                    }
                }

                if (w == indices.Length) break;
            }

            // Add remaining points that weren't selected at any level
            for (var i = 0; i < indices.Length; i++)
                if (used[i] == 0)
                    indices[w++] = indices[i];
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(used);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long CellKey(int x, int y, int z)
    {
        return ((long)(uint)(x & 0x1FFFFF) << 42) | ((long)(uint)(y & 0x1FFFFF) << 21) | (uint)(z & 0x1FFFFF);
    }

    private static bool TryGetPositions(
        Dictionary<string, float[]> ply,
        out float[] x, out float[] y, out float[] z)
    {
        x = y = z = Array.Empty<float>();
        if (ply == null) return false;

        return TryGetCaseInsensitive(ply, "x", out x) &&
               TryGetCaseInsensitive(ply, "y", out y) &&
               TryGetCaseInsensitive(ply, "z", out z);
    }

    private static bool TryGetCaseInsensitive(Dictionary<string, float[]> dict, string key, out float[] arr)
    {
        foreach (var kv in dict)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                arr = kv.Value;
                return true;
            }
        }

        arr = Array.Empty<float>();
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Hash32(int x)
    {
        unchecked
        {
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return x < 0 ? -x : x;
        }
    }

    private static void ShuffleDeterministic(Span<int> span, int seed)
    {
        var n = span.Length;
        var s = Hash32(seed);
        for (var i = n - 1; i > 0; --i)
        {
            s = unchecked(s * 1664525 + 1013904223);
            var j = (int)((uint)s % (uint)(i + 1));
            (span[i], span[j]) = (span[j], span[i]);
        }
    }

    #region Internal Structures

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct CpuNode
    {
        public Vector3 BoundsMin;
        public float BoundsSize;
        public Vector3 BoundsMax;
        public uint ChildStartIndex;
        public uint IndexStartIndex;
        public uint IndexCount;
        public uint LODLevel;
        public uint ParentIndex;

        public bool IsLeaf =>
            ChildStartIndex == 0xFFFFFFFFu || unchecked((int)ChildStartIndex) == -1;

        public Bounds ToBounds() => new() { Min = BoundsMin, Max = BoundsMax };
    }

    internal struct Bounds
    {
        public Vector3 Min, Max;
        public Vector3 Size => Max - Min;
    }

    internal readonly struct BoundsKeySeed
    {
        public static int FromBounds(in Bounds b)
        {
            var hx = BitConverter.SingleToInt32Bits(b.Min.X);
            var hy = BitConverter.SingleToInt32Bits(b.Min.Y);
            var hz = BitConverter.SingleToInt32Bits(b.Min.Z);
            var HX = BitConverter.SingleToInt32Bits(b.Max.X);
            var HY = BitConverter.SingleToInt32Bits(b.Max.Y);
            var HZ = BitConverter.SingleToInt32Bits(b.Max.Z);
            return Hash32(hx ^ hy ^ hz ^ HX ^ HY ^ HZ);
        }
    }

    #endregion
}
