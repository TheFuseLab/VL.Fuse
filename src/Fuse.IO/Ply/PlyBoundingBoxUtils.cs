using System.Runtime.InteropServices;
using Stride.Core.Mathematics;

namespace Fuse.IO.Ply;

internal static class PlyBoundingBoxUtils
{
    public static bool TryComputeFromSoA(Dictionary<string, float[]> data, out BoundingBox boundingBox)
    {
        boundingBox = default;
        if (data == null || data.Count == 0)
            return false;
        if (!TryGetCaseInsensitive(data, "x", out var xs) ||
            !TryGetCaseInsensitive(data, "y", out var ys) ||
            !TryGetCaseInsensitive(data, "z", out var zs))
            return false;
        if (xs == null || ys == null || zs == null)
            return false;
        if (xs.Length == 0 || ys.Length == 0 || zs.Length == 0)
            return false;

        var count = Math.Min(xs.Length, Math.Min(ys.Length, zs.Length));
        if (count <= 0)
            return false;

        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var minZ = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        var maxZ = float.MinValue;

        for (var i = 0; i < count; i++)
        {
            var x = xs[i];
            var y = ys[i];
            var z = zs[i];
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }

        boundingBox = new BoundingBox(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
        return true;
    }

    public static bool TryComputeFromOctreeRoot(byte[] nodeBufferData, out BoundingBox boundingBox)
    {
        boundingBox = default;
        if (nodeBufferData == null || nodeBufferData.Length == 0)
            return false;

        var nodeSize = Marshal.SizeOf<GPUOctree.GPUOctreeNode>();
        if (nodeSize <= 0 || nodeBufferData.Length < nodeSize)
            return false;

        var nodes = MemoryMarshal.Cast<byte, GPUOctree.GPUOctreeNode>(nodeBufferData.AsSpan());
        if (nodes.Length == 0)
            return false;

        var root = nodes[0];
        boundingBox = new BoundingBox(
            new Vector3(root.BoundsMinX, root.BoundsMinY, root.BoundsMinZ),
            new Vector3(root.BoundsMaxX, root.BoundsMaxY, root.BoundsMaxZ));
        return true;
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
}

