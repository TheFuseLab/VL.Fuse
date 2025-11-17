using System.Runtime.InteropServices;

#pragma warning disable CS1591

// High-performance octree optimized for memory efficiency and cache performance
namespace Fuse.IO.Ply;

public class GPUOctree
{
    #region Public Structures and Configuration

    public class OctreeProgressInfo
    {
        public string? StageName { get; set; }
        public double ProgressPercentage { get; set; }
        public bool IsCompleted { get; set; }
        public Exception? Error { get; set; }
        public byte[]? NodeBufferData { get; set; }
        public byte[]? IndexBufferData { get; set; }
        public int NodeCount { get; set; }
        public int TotalNodes { get; set; }
        public int IndexCount { get; set; }
        public long TotalMemoryUsed { get; set; }
        public int TotalPoints { get; set; }
        public int NodesProcessed { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GPUOctreeNode
    {
        public float BoundsMinX, BoundsMinY, BoundsMinZ;
        public float BoundsSize;
        public float BoundsMaxX, BoundsMaxY, BoundsMaxZ;
        public int ChildStartIndex;
        public int IndexStartIndex;
        public int IndexCount;
        public int LODLevel;
        public int ParentIndex;
    }

    public class BuildConfig
    {
        public bool EnableDetailedValidation = false;
        public int MaxDepth = 10;
        public int MaxPointsPerLeaf = 2048;
        public bool OptimizeForSpeed = true; // Enable all speed optimizations
        public int ProgressReportInterval = 100000; // More frequent for faster builds
        public float SubdivisionEpsilon = 1e-4f;
    }

    #endregion

    #region Internal Structures and Members

    private struct OctreeNode
    {
        public float MinX, MinY, MinZ;
        public float MaxX, MaxY, MaxZ;
        public int FirstChildIndex;
        public int StartIndex;
        public int PointCount;
        public int Depth;
        public int ParentIndex;
    }

    private List<OctreeNode> _nodes = new(0);
    private int[] _pointIndices = Array.Empty<int>();
    private float[] _xCoords = Array.Empty<float>(), _yCoords = Array.Empty<float>(), _zCoords = Array.Empty<float>();
    private int _totalPoints;
    private BuildConfig _config = new();

    // Reusable arrays to avoid allocations
    private int[] _tempOctants = Array.Empty<int>();
    private int[] _octantCounts = Array.Empty<int>();
    private int[] _octantOffsets = Array.Empty<int>();

    #endregion

    #region Main Build Process

    public static async Task BuildInBackgroundAsync(
        Dictionary<string, float[]> plyData,
        OctreeProgressInfo octreeProgressInfo,
        BuildConfig? config = null)
    {
        var octree = new GPUOctree();
        await Task.Run(() => octree.BuildInternal(plyData, octreeProgressInfo, config ?? new BuildConfig()));
    }

    private void BuildInternal(
        Dictionary<string, float[]> plyData,
        OctreeProgressInfo octreeProgressInfo,
        BuildConfig config)
    {
        var startTime = DateTime.Now;

        try
        {
            _config = config;

            _xCoords = plyData["x"];
            _yCoords = plyData["y"];
            _zCoords = plyData["z"];
            _totalPoints = _xCoords.Length;
            octreeProgressInfo.TotalPoints = _totalPoints;
            Console.WriteLine($"Building high-performance octree for {_totalPoints:N0} points...");

            // Pre-allocate reusable arrays
            if (_config.OptimizeForSpeed)
            {
                _tempOctants = new int[Math.Min(_totalPoints, 1000000)]; // Cap at 1M for memory
                _octantCounts = new int[8];
                _octantOffsets = new int[8];
            }

            // Auto-adjust epsilon
            var bounds = CalculateBounds();
            var dataScale = Math.Max(Math.Max(bounds.maxX - bounds.minX, bounds.maxY - bounds.minY),
                bounds.maxZ - bounds.minZ);

            if (_config.SubdivisionEpsilon == 1e-4f) _config.SubdivisionEpsilon = dataScale * 1e-7f;

            // Initialize point indices
            _pointIndices = new int[_totalPoints];
            for (var i = 0; i < _totalPoints; i++) _pointIndices[i] = i;

            // Pre-allocate nodes list with reasonable capacity
            _nodes = new List<OctreeNode>(Math.Min(_totalPoints / 100, 500000));
            _nodes.Add(new OctreeNode
            {
                MinX = bounds.minX, MinY = bounds.minY, MinZ = bounds.minZ,
                MaxX = bounds.maxX, MaxY = bounds.maxY, MaxZ = bounds.maxZ,
                FirstChildIndex = -1, StartIndex = 0, PointCount = _totalPoints,
                Depth = 0, ParentIndex = -1
            });

            // Use breadth-first processing for better cache behavior
            var currentLevel = new Queue<int>();
            var nextLevel = new Queue<int>();
            currentLevel.Enqueue(0);

            var nodesProcessedCounter = 0;
            var currentDepth = 0;

            while (currentLevel.Count > 0 || nextLevel.Count > 0)
            {
                if (currentLevel.Count == 0)
                {
                    // Move to next level
                    var temp = currentLevel;
                    currentLevel = nextLevel;
                    nextLevel = temp;
                    currentDepth++;
                    Console.WriteLine($"Processing depth {currentDepth}, {currentLevel.Count} nodes");
                }

                var nodeID = currentLevel.Dequeue();
                nodesProcessedCounter++;

                if (nodesProcessedCounter % _config.ProgressReportInterval == 0)
                {
                    var elapsed = DateTime.Now - startTime;
                    Console.WriteLine(
                        $"Processed {nodesProcessedCounter:N0} nodes in {elapsed.TotalSeconds:F1}s, {_nodes.Count:N0} total nodes");
                }

                var currentNode = _nodes[nodeID];

                if (currentNode.PointCount > _config.MaxPointsPerLeaf && currentNode.Depth < _config.MaxDepth)
                    if (SubdivideOptimized(nodeID))
                    {
                        var firstChildID = _nodes[nodeID].FirstChildIndex;
                        for (var i = 0; i < 8; i++)
                        {
                            var childNode = _nodes[firstChildID + i];
                            if (childNode.PointCount > 0) nextLevel.Enqueue(firstChildID + i);
                        }
                    }
            }

            var buildTime = DateTime.Now - startTime;
            Console.WriteLine($"Octree complete: {_nodes.Count:N0} nodes in {buildTime.TotalSeconds:F1} seconds");

            if (_config.EnableDetailedValidation)
                FinalVerification(octreeProgressInfo);
            else
                QuickValidation();

            PackForGPU(octreeProgressInfo);
            octreeProgressInfo.IsCompleted = true;
        }
        catch (Exception ex)
        {
            octreeProgressInfo.Error = ex;
            octreeProgressInfo.IsCompleted = true;
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }

    #endregion

    #region High-Performance Core Logic

    private bool SubdivideOptimized(int parentNodeID)
    {
        var parentNode = _nodes[parentNodeID];
        var firstChildID = _nodes.Count;

        // Calculate center point
        var cX = (parentNode.MinX + parentNode.MaxX) * 0.5f;
        var cY = (parentNode.MinY + parentNode.MaxY) * 0.5f;
        var cZ = (parentNode.MinZ + parentNode.MaxZ) * 0.5f;

        // Fast in-place partitioning instead of creating temporary lists
        if (_config.OptimizeForSpeed && parentNode.PointCount <= _tempOctants.Length)
            return SubdivideInPlace(parentNodeID, parentNode, firstChildID, cX, cY, cZ);

        return SubdivideStandard(parentNodeID, parentNode, firstChildID, cX, cY, cZ);
    }

    private bool SubdivideInPlace(int parentNodeID, OctreeNode parentNode, int firstChildID, float cX, float cY,
        float cZ)
    {
        // Clear counters
        Array.Clear(_octantCounts, 0, 8);

        // First pass: classify points and count
        for (var i = 0; i < parentNode.PointCount; i++)
        {
            var arrayIndex = parentNode.StartIndex + i;
            var pIdx = _pointIndices[arrayIndex];

            var px = Math.Max(parentNode.MinX, Math.Min(parentNode.MaxX, _xCoords[pIdx]));
            var py = Math.Max(parentNode.MinY, Math.Min(parentNode.MaxY, _yCoords[pIdx]));
            var pz = Math.Max(parentNode.MinZ, Math.Min(parentNode.MaxZ, _zCoords[pIdx]));

            var octant = 0;
            if (px >= cX) octant |= 1;
            if (py >= cY) octant |= 2;
            if (pz >= cZ) octant |= 4;

            _tempOctants[i] = octant;
            _octantCounts[octant]++;
        }

        // Check if subdivision is effective
        var nonEmptyOctants = 0;
        for (var i = 0; i < 8; i++)
            if (_octantCounts[i] > 0)
                nonEmptyOctants++;

        if (nonEmptyOctants <= 1) return false;

        // Calculate offsets for in-place partitioning
        _octantOffsets[0] = parentNode.StartIndex;
        for (var i = 1; i < 8; i++) _octantOffsets[i] = _octantOffsets[i - 1] + _octantCounts[i - 1];

        // In-place partitioning using counting sort approach
        var tempIndices = new int[parentNode.PointCount];
        var tempWriteOffsets = new int[8];
        Array.Copy(_octantOffsets, tempWriteOffsets, 8);

        // Rearrange points based on octant classification
        for (var i = 0; i < parentNode.PointCount; i++)
        {
            var octant = _tempOctants[i];
            var originalIndex = parentNode.StartIndex + i;
            var newIndex = tempWriteOffsets[octant] - parentNode.StartIndex;
            tempIndices[newIndex] = _pointIndices[originalIndex];
            tempWriteOffsets[octant]++;
        }

        // Copy back to main array
        Array.Copy(tempIndices, 0, _pointIndices, parentNode.StartIndex, parentNode.PointCount);

        // Create child nodes
        CreateChildNodesOptimized(parentNodeID, parentNode, firstChildID, cX, cY, cZ);
        return true;
    }

    private bool SubdivideStandard(int parentNodeID, OctreeNode parentNode, int firstChildID, float cX, float cY,
        float cZ)
    {
        // Fallback to standard method for very large nodes
        var childPointLists = new List<int>[8];
        var estimatedChildSize = Math.Max(16, parentNode.PointCount / 8);

        for (var i = 0; i < 8; i++) childPointLists[i] = new List<int>(estimatedChildSize);

        for (var i = 0; i < parentNode.PointCount; i++)
        {
            var arrayIndex = parentNode.StartIndex + i;
            var pIdx = _pointIndices[arrayIndex];

            var px = Math.Max(parentNode.MinX, Math.Min(parentNode.MaxX, _xCoords[pIdx]));
            var py = Math.Max(parentNode.MinY, Math.Min(parentNode.MaxY, _yCoords[pIdx]));
            var pz = Math.Max(parentNode.MinZ, Math.Min(parentNode.MaxZ, _zCoords[pIdx]));

            var octant = 0;
            if (px >= cX) octant |= 1;
            if (py >= cY) octant |= 2;
            if (pz >= cZ) octant |= 4;

            childPointLists[octant].Add(pIdx);
        }

        var nonEmptyOctants = 0;
        for (var i = 0; i < 8; i++)
            if (childPointLists[i].Count > 0)
                nonEmptyOctants++;

        if (nonEmptyOctants <= 1) return false;

        CreateChildNodesStandard(parentNodeID, parentNode, firstChildID, childPointLists, cX, cY, cZ);
        return true;
    }

    private void CreateChildNodesOptimized(int parentNodeID, OctreeNode parentNode, int firstChildID, float cX,
        float cY, float cZ)
    {
        var expansion = _config.SubdivisionEpsilon;

        for (var i = 0; i < 8; i++)
        {
            var pointCount = _octantCounts[i];

            // Calculate child bounds
            var minX = (i & 1) == 0 ? parentNode.MinX : cX;
            var maxX = (i & 1) == 0 ? cX : parentNode.MaxX;
            var minY = (i & 2) == 0 ? parentNode.MinY : cY;
            var maxY = (i & 2) == 0 ? cY : parentNode.MaxY;
            var minZ = (i & 4) == 0 ? parentNode.MinZ : cZ;
            var maxZ = (i & 4) == 0 ? cZ : parentNode.MaxZ;

            // Expand bounds slightly
            minX = Math.Max(minX - expansion, parentNode.MinX - expansion);
            maxX = Math.Min(maxX + expansion, parentNode.MaxX + expansion);
            minY = Math.Max(minY - expansion, parentNode.MinY - expansion);
            maxY = Math.Min(maxY + expansion, parentNode.MaxY + expansion);
            minZ = Math.Max(minZ - expansion, parentNode.MinZ - expansion);
            maxZ = Math.Min(maxZ + expansion, parentNode.MaxZ + expansion);

            var childStartIndex = pointCount > 0 ? _octantOffsets[i] : parentNode.StartIndex;

            _nodes.Add(new OctreeNode
            {
                MinX = minX, MaxX = maxX,
                MinY = minY, MaxY = maxY,
                MinZ = minZ, MaxZ = maxZ,
                FirstChildIndex = -1,
                Depth = parentNode.Depth + 1,
                ParentIndex = parentNodeID,
                StartIndex = childStartIndex,
                PointCount = pointCount
            });
        }

        // Update parent to internal node
        var updatedParent = parentNode;
        updatedParent.PointCount = 0;
        updatedParent.FirstChildIndex = firstChildID;
        _nodes[parentNodeID] = updatedParent;
    }

    private void CreateChildNodesStandard(int parentNodeID, OctreeNode parentNode, int firstChildID,
        List<int>[] childPointLists, float cX, float cY, float cZ)
    {
        var writeOffset = parentNode.StartIndex;
        var expansion = _config.SubdivisionEpsilon;

        for (var i = 0; i < 8; i++)
        {
            var pointList = childPointLists[i];

            var minX = (i & 1) == 0 ? parentNode.MinX : cX;
            var maxX = (i & 1) == 0 ? cX : parentNode.MaxX;
            var minY = (i & 2) == 0 ? parentNode.MinY : cY;
            var maxY = (i & 2) == 0 ? cY : parentNode.MaxY;
            var minZ = (i & 4) == 0 ? parentNode.MinZ : cZ;
            var maxZ = (i & 4) == 0 ? cZ : parentNode.MaxZ;

            minX = Math.Max(minX - expansion, parentNode.MinX - expansion);
            maxX = Math.Min(maxX + expansion, parentNode.MaxX + expansion);
            minY = Math.Max(minY - expansion, parentNode.MinY - expansion);
            maxY = Math.Min(maxY + expansion, parentNode.MaxY + expansion);
            minZ = Math.Max(minZ - expansion, parentNode.MinZ - expansion);
            maxZ = Math.Min(maxZ + expansion, parentNode.MaxZ + expansion);

            var childStartIndex = pointList.Count > 0 ? writeOffset : parentNode.StartIndex;

            if (pointList.Count > 0)
            {
                pointList.CopyTo(_pointIndices, writeOffset);
                writeOffset += pointList.Count;
            }

            _nodes.Add(new OctreeNode
            {
                MinX = minX, MaxX = maxX,
                MinY = minY, MaxY = maxY,
                MinZ = minZ, MaxZ = maxZ,
                FirstChildIndex = -1,
                Depth = parentNode.Depth + 1,
                ParentIndex = parentNodeID,
                StartIndex = childStartIndex,
                PointCount = pointList.Count
            });
        }

        var updatedParent = parentNode;
        updatedParent.PointCount = 0;
        updatedParent.FirstChildIndex = firstChildID;
        _nodes[parentNodeID] = updatedParent;
    }

    private void QuickValidation()
    {
        var totalLeafPoints = 0;
        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            if (node.FirstChildIndex == -1 && node.PointCount > 0) totalLeafPoints += node.PointCount;
        }

        if (totalLeafPoints != _totalPoints)
            throw new Exception($"Point count mismatch: {totalLeafPoints} != {_totalPoints}");
        Console.WriteLine("Quick validation passed");
    }

    private void FinalVerification(OctreeProgressInfo progress)
    {
        progress.StageName = "Verifying data integrity...";
        Console.WriteLine(progress.StageName);
        progress.ProgressPercentage = 90;

        var totalLeafPoints = 0;
        var allPointsUsed = new HashSet<int>();

        for (var i = 0; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            if (node.FirstChildIndex == -1 && node.PointCount > 0)
            {
                totalLeafPoints += node.PointCount;

                for (var j = 0; j < node.PointCount; j++)
                {
                    var pointIndex = _pointIndices[node.StartIndex + j];

                    if (pointIndex < 0 || pointIndex >= _totalPoints)
                        throw new Exception($"Invalid point index: {pointIndex} in node {i}");

                    if (allPointsUsed.Contains(pointIndex))
                        throw new Exception($"Duplicate point: {pointIndex} in node {i}");
                    allPointsUsed.Add(pointIndex);
                }
            }
        }

        if (totalLeafPoints != _totalPoints)
            throw new Exception($"Point count mismatch: {totalLeafPoints} != {_totalPoints}");

        Console.WriteLine("Detailed verification passed");
    }

    private void PackForGPU(OctreeProgressInfo progress)
    {
        progress.StageName = "Packing for GPU...";
        progress.ProgressPercentage = 95;
        progress.NodeCount = _nodes.Count;
        progress.TotalNodes = _nodes.Count;

        var gpuNodes = new GPUOctreeNode[progress.NodeCount];

        for (var i = 0; i < progress.NodeCount; i++)
        {
            var node = _nodes[i];
            var isLeaf = node.FirstChildIndex == -1;
            gpuNodes[i] = new GPUOctreeNode
            {
                BoundsMinX = node.MinX, BoundsMinY = node.MinY, BoundsMinZ = node.MinZ,
                BoundsMaxX = node.MaxX, BoundsMaxY = node.MaxY, BoundsMaxZ = node.MaxZ,
                BoundsSize = Math.Max(Math.Max(node.MaxX - node.MinX, node.MaxY - node.MinY), node.MaxZ - node.MinZ),
                LODLevel = node.Depth,
                ChildStartIndex = node.FirstChildIndex,
                ParentIndex = node.ParentIndex,
                IndexStartIndex = isLeaf ? node.StartIndex : 0,
                IndexCount = isLeaf ? node.PointCount : 0
            };
        }

        progress.NodeBufferData = ConvertStructArrayToBytes(gpuNodes);
        progress.IndexBufferData = ConvertIntArrayToBytes(_pointIndices);
        progress.IndexCount = _pointIndices.Length;
        progress.TotalMemoryUsed = progress.NodeBufferData.Length + progress.IndexBufferData.Length;

        Console.WriteLine(
            $"Packed {progress.NodeBufferData.Length / (1024.0 * 1024.0):F1} MB nodes, {progress.IndexBufferData.Length / (1024.0 * 1024.0):F1} MB indices");
    }

    private (float minX, float minY, float minZ, float maxX, float maxY, float maxZ) CalculateBounds()
    {
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

        for (var i = 0; i < _totalPoints; i++)
        {
            float x = _xCoords[i], y = _yCoords[i], z = _zCoords[i];
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
            if (z < minZ) minZ = z;
            if (z > maxZ) maxZ = z;
        }

        var centerX = (minX + maxX) * 0.5f;
        var centerY = (minY + maxY) * 0.5f;
        var centerZ = (minZ + maxZ) * 0.5f;
        var maxDimension = Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ);
        var padding = maxDimension * 0.001f;
        var halfSize = (maxDimension + padding) * 0.5f;

        return (centerX - halfSize, centerY - halfSize, centerZ - halfSize,
            centerX + halfSize, centerY + halfSize, centerZ + halfSize);
    }

    private byte[] ConvertStructArrayToBytes<T>(T[] structures) where T : struct
    {
        var bufferSize = structures.Length * Marshal.SizeOf<T>();
        var buffer = new byte[bufferSize];
        var handle = GCHandle.Alloc(structures, GCHandleType.Pinned);
        try
        {
            Marshal.Copy(handle.AddrOfPinnedObject(), buffer, 0, bufferSize);
        }
        finally
        {
            handle.Free();
        }

        return buffer;
    }

    private byte[] ConvertIntArrayToBytes(int[] data)
    {
        if ((long)data.Length * sizeof(int) > 2L * 1024 * 1024 * 1024)
            throw new Exception("Index buffer too large for current implementation");
        var buffer = new byte[data.Length * sizeof(int)];
        Buffer.BlockCopy(data, 0, buffer, 0, buffer.Length);
        return buffer;
    }

    #endregion
}