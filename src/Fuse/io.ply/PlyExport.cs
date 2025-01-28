using System;
using System.Collections.Generic;
using System.IO;
using Fuse.util;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;

namespace Fuse.io.ply;

public static class PlyExport
{
    /// <summary>
    /// Exports multiple vertex-data arrays as a single binary PLY, 
    /// after transforming each vertex's position and culling via sphere-frustum intersection.
    /// 
    /// Requirements in elementNames:
    ///  - "x", "y", "z" for position
    ///  - "radius" for the bounding sphere radius
    ///  (These attributes can be in any order, but must exist in the list.)
    ///  Any additional attributes (e.g. "nx", "ny", "nz", "u", "v") will be preserved.
    /// 
    /// Each float[] in vertexDataList must have the same layout as elementNames.
    /// Each Matrix in transforms must correspond to the same index in vertexDataList.
    /// </summary>
    /// <param name="vertexDataList">
    ///   A list of float arrays, each representing a set of vertices 
    ///   (attributes interleaved: x, y, z, nx, ny, nz, radius, etc.).
    /// </param>
    /// <param name="transforms">
    ///   A list of 4x4 transform matrices to apply to each float[] in vertexDataList.
    ///   Must be the same length as vertexDataList.
    /// </param>
    /// <param name="elementNames">
    ///   A list of attribute names, one per float. Must include "x", "y", "z", "radius".
    ///   Example: [ "x", "y", "z", "nx", "ny", "nz", "radius" ].
    /// </param>
    /// <param name="frustum">
    ///   The Stride BoundingFrustum used for culling. Only spheres that intersect the frustum are kept.
    /// </param>
    /// <param name="radius">apply sphere radius to the points</param>
    /// <param name="outputPlyPath">
    ///   The output path for the final, merged binary PLY file.
    /// </param>
    /// <param name="checkFrustum">enable to apply frustum culling</param>
    public static void ExportBinaryPlyWithSphereFrustumCullingAndTransforms(
        List<float[]> vertexDataList,
        List<Matrix>? transforms,
        IReadOnlyList<string> elementNames,
        BoundingFrustum frustum,
        bool checkFrustum,
        float radius,
        string outputPlyPath)
    {
        // --- 1) Validate Inputs
        if (vertexDataList == null || vertexDataList.Count == 0)
            throw new ArgumentException("vertexDataList is null or empty.");

        var applyTransforms = !(transforms == null || transforms.Count != vertexDataList.Count);

        if (elementNames == null || elementNames.Count < 4)
            throw new ArgumentException("elementNames must contain at least x, y, z, and radius attributes.");

        // Find attribute indices
        var xIndex      = elementNames.IndexOf("x");
        var yIndex      = elementNames.IndexOf("y");
        var zIndex      = elementNames.IndexOf("z");
        if (xIndex < 0 || yIndex < 0 || zIndex < 0)
            throw new ArgumentException("elementNames must include 'x', 'y', 'z', attributes.");

        var stride = elementNames.Count;  // floats per vertex

        // --- 2) Extract frustum planes once
        var planes = checkFrustum ? FrustumUtil.GetFrustumPlanes(frustum):null;
        var sphere = new BoundingSphere(new Vector3(), radius);
        var position = new Vector3();

        // We’ll store the final, culled, and transformed vertices here
        var mergedKeptVertices = new List<float>();

        // --- 3) Process Each Vertex Array + Its Transform
        for (var arrayIdx = 0; arrayIdx < vertexDataList.Count; arrayIdx++)
        {
            var vertexData = vertexDataList[arrayIdx];
            var transform   = applyTransforms ? transforms[arrayIdx] : Matrix.Identity;

            if (vertexData == null || vertexData.Length == 0)
                continue; // or throw? up to you

            if (vertexData.Length % stride != 0)
            {
                throw new ArgumentException(
                    $"vertexData at index {arrayIdx} length ({vertexData.Length}) " +
                    $"is not divisible by stride ({stride})."
                );
            }

            var vertexCount = vertexData.Length / stride;

            // For each vertex in this array, transform (x, y, z), do sphere culling
            for (var i = 0; i < vertexCount; i++)
            {
                var baseIndex = i * stride;

                // Original local position
                var localX = vertexData[baseIndex + xIndex];
                var localY = vertexData[baseIndex + yIndex];
                var localZ = vertexData[baseIndex + zIndex];
                var localPos = new Vector3(localX, localY, localZ);

                // Transform this position
                var worldPos = applyTransforms ? Vector4.Transform(new Vector4(localPos, 1f), transform) : new Vector4(localPos, 1f);
                    
                // Convert back to Vector3 for culling & storage

                // Sphere radius
                if (radius < 0f)
                {
                    // optional: skip or clamp
                    Console.WriteLine($"Warning: negative radius {radius} at vertex {i} (arrayIdx={arrayIdx}).");
                }

                // Construct the bounding sphere in world space
                // If your transform includes scaling, you might want to scale radius accordingly.
                // The position is in the first three attributes: x, y, z
                position.X = worldPos.X;
                position.Y = worldPos.Y;
                position.Z = worldPos.Z;
            

                // Check if inside frustum
                sphere.Center = position;

                // Check if sphere intersects frustum
                if (checkFrustum && ! FrustumUtil.IsSphereInsideFrustum(planes,sphere))
                    continue;

                // If inside, copy the entire vertex attribute block
                // but replace the old x,y,z with the transformed x,y,z
                // (Everything else stays the same, e.g. normal, radius, color, etc.)
                var start = mergedKeptVertices.Count; 
                mergedKeptVertices.AddRange(new float[stride]); // reserve space

                // Copy attributes
                // We'll do it attribute by attribute (or you could copy a chunk and overwrite x,y,z).
                for (var k = 0; k < stride; k++)
                {
                    mergedKeptVertices[start + k] = vertexData[baseIndex + k];
                }

                // Overwrite x,y,z with transformed
                mergedKeptVertices[start + xIndex] = worldPos.X;
                mergedKeptVertices[start + yIndex] = worldPos.Y;
                mergedKeptVertices[start + zIndex] = worldPos.Z;
            }
        }

        // --- 4) Write Merged Binary PLY
        int finalVertexCount = mergedKeptVertices.Count / stride;

        using (var fs = new FileStream(outputPlyPath, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(fs))
        {
            // -- ASCII part of the header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("ply\n"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("format binary_little_endian 1.0\n"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes($"element vertex {finalVertexCount}\n"));

            // For each attribute name, write "property float <name>"
            foreach (string attr in elementNames)
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes($"property float {attr}\n"));
            }

            writer.Write(System.Text.Encoding.ASCII.GetBytes("end_header\n"));

            // -- Binary vertex data
            // Each vertex has 'stride' floats
            for (int i = 0; i < mergedKeptVertices.Count; i++)
            {
                float val = mergedKeptVertices[i];
                writer.Write(val);
            }
        }

        Console.WriteLine($"Merged PLY export complete. Wrote {finalVertexCount} vertices to '{outputPlyPath}'.");
    }
}