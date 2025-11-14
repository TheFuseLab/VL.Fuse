#nullable enable
using System;
using Stride.Core.Mathematics;
using ComputeViewPosition = System.Func<Stride.Core.Mathematics.Vector3, Stride.Core.Mathematics.Vector3, Stride.Core.Mathematics.Matrix, Stride.Core.Mathematics.Vector3>;

namespace Fuse.util;

public static class BoxGridCull
{
    // Function to map a point on a room surface to texture coordinates
    private static Vector4 mapToTexture(
        Vector3 pointOnSurface,
        int surfaceIndex,
        // Buffer data passed as parameters
        Vector4[] faceLayoutInfo,
        Vector3 roomCenter,
        Vector3 roomDimensions,
        // Zoom parameter (optional)
        Vector4 zoomRegion // Region to zoom into: (minU, minV, maxU, maxV) in normalized cross layout space
    )
    {
        // Convert point to local room coordinates (relative to room's min corner)
        var localPoint = pointOnSurface - (roomCenter - roomDimensions * 0.5f);

        var u = 0.0f;
        var v = 0.0f;

        // Map based on the surface index
        switch (surfaceIndex)
        {
            case 0: // Floor (Green)
                u = localPoint.X / roomDimensions.X;
                v = 1.0f - localPoint.Z / roomDimensions.Z;
                break;

            case 1: // Ceiling (Yellow)
                u = localPoint.X / roomDimensions.X;
                v = localPoint.Z / roomDimensions.Z;
                break;

            case 2: // Right wall (Magenta)
                u = localPoint.Z / roomDimensions.Z;
                v = localPoint.Y / roomDimensions.Y;
                break;

            case 3: // Back wall (Cyan)
                u = 1.0f - localPoint.X / roomDimensions.X;
                v = localPoint.Y / roomDimensions.Y;
                break;

            case 4: // Left wall (Red)
                u = 1.0f - localPoint.Z / roomDimensions.Z;
                v = localPoint.Y / roomDimensions.Y;
                break;

            case 5: // Front wall (Blue)
                u = localPoint.X / roomDimensions.X;
                v = localPoint.Y / roomDimensions.Y;
                break;

            default:
                // Default case (shouldn't happen)
                u = v = 0.0f;
                break;
        }

        // Store normalized per-face UVs in [0,1]
        var faceUV01 = new Vector2(u, v);

        // Get layout info for this face (startX, startY, width, height)
        var layout = faceLayoutInfo[surfaceIndex];

        // Map normalized coordinates to the cross layout
        Vector2 texCoord;
        texCoord.X = layout.X + u * layout.Z;
        texCoord.Y = layout.Y + v * layout.W;

        // Normalize coordinates
        texCoord = texCoord / new Vector2(4.0f, 3.0f);

        texCoord = new Vector2(
            (texCoord.X - zoomRegion.X) / (zoomRegion.Z - zoomRegion.X),
            (texCoord.Y - zoomRegion.Y) / (zoomRegion.W - zoomRegion.Y)
        );

        // Scale back to cross layout size
        texCoord = texCoord * new Vector2(4.0f, 3.0f);

        // Return atlas UVs (xy) + face-local UVs (zw)
        return new Vector4(texCoord.X, texCoord.Y, faceUV01.X, faceUV01.Y);
    }
    /// <summary>
    ///     CPU equivalent of projectParticleByViewPositionAABB + mapToTexture.
    ///     Returns false if ray from ViewerPosition through particlePos does not hit the room or visible atlas region.
    /// </summary>
    public static bool projectParticleByViewPositionAABB(
        Vector3 particlePos,
        Matrix transform,
        Vector3 roomCenter,
        Vector3 roomDimensions,
        Vector4 zoomRegion, // same semantics as before
        Vector3 viewerPosition,
        out Vector3 ProjectedPosition,
        out int FaceIndex,
        out Vector2 FaceUV,
        out float Depth,
        out Vector2 UV,
        Vector4[] faceLayoutInfo,
        Vector4 crossLayoutInfo,
        ComputeViewPosition? computeViewPosition = null
    )
    {
        // safe defaults so HLSL is happy
        ProjectedPosition = new Vector3(0.0f, 0.0f, 0.0f);
        FaceIndex = -1;
        FaceUV = new Vector2(0.0f, 0.0f);
        Depth = 0.0f;
        UV = new Vector2(0.0f, 0.0f);
        
        var viewerPosPre = Vector3.Transform(viewerPosition, Matrix.Invert(transform)).XYZ();
        
        var ro = computeViewPosition != null
            ? computeViewPosition(particlePos, viewerPosPre, transform)
            : viewerPosition;
        
        particlePos = Vector3.Transform(particlePos, transform).XYZ();
        
        var rd = Vector3.Normalize(particlePos - ro);

        var halfExt = roomDimensions * 0.5f;
        var boxMin = roomCenter - halfExt;
        var boxMax = roomCenter + halfExt;

        // ---------- slab intersection ----------
        var invDir = 1.0f / rd;

        var t0 = (boxMin - ro) * invDir;
        var t1 = (boxMax - ro) * invDir;

        var tmin = Vector3.Min(t0, t1);
        var tmax = Vector3.Max(t0, t1);

        var tEnter = System.Math.Max(System.Math.Max(tmin.X, tmin.Y), tmin.Z);
        var tExit = System.Math.Min(System.Math.Min(tmax.X, tmax.Y), tmax.Z);

        // no hit or behind
        if (tExit < 0.0 || tEnter > tExit)
            return false;

        var t = tEnter > 0.0 ? tEnter : tExit;
        if (t <= 0.0)
            return false;

        var p = ro + rd * t;

        // ---------- classify face (match your indices) ----------
        const float eps = 1e-4f;

        // 0: floor (Green)    -> y = boxMin.Y
        // 1: ceiling (Yellow) -> y = boxMax.Y
        // 2: right (Magenta)  -> x = boxMax.X
        // 3: back (Cyan)      -> z = boxMax.Z
        // 4: left (Red)       -> x = boxMin.X
        // 5: front (Blue)     -> z = boxMin.Z

        if (System.Math.Abs(p.Y - boxMin.Y) < eps) FaceIndex = 0;
        else if (System.Math.Abs(p.Y - boxMax.Y) < eps) FaceIndex = 1;
        else if (System.Math.Abs(p.X - boxMax.X) < eps) FaceIndex = 2;
        else if (System.Math.Abs(p.Z - boxMax.Z) < eps) FaceIndex = 3;
        else if (System.Math.Abs(p.X - boxMin.X) < eps) FaceIndex = 4;
        else if (System.Math.Abs(p.Z - boxMin.Z) < eps) FaceIndex = 5;

        if (FaceIndex < 0)
            return false;

        // ---------- UV & atlas via your existing mapToTexture ----------
        // mapToTexture expects the exact hit point, face index, and your original buffers.
        var uvPack = mapToTexture(
            p,
            FaceIndex,
            faceLayoutInfo,
            roomCenter,
            roomDimensions,
            zoomRegion
        );

        FaceUV = new Vector2(uvPack.Z, uvPack.W);

        // IMPORTANT: z matches original: distance from intersection to particle
        var offsetDist = Vector3.Distance(p, particlePos);

        Depth = offsetDist;
        UV = new Vector2(uvPack.X / crossLayoutInfo.X, 1 - uvPack.Y / crossLayoutInfo.Y);
        // uvPack.xy is in 4x3 layout space, like before
        ProjectedPosition = new Vector3(uvPack.X, uvPack.Y, offsetDist);

        if (UV.X >= 0.0 && UV.X <= 1.0 && UV.Y >= 0.0 && UV.Y <= 1.0) return true;
        
        FaceIndex = -1;
        return false;
    }

    private static bool SphereIntersectsAabb(in BoundingSphere s, in BoundingBox b)
    {
        var c = s.Center;
        var x = MathF.Max(b.Minimum.X, MathF.Min(c.X, b.Maximum.X));
        var y = MathF.Max(b.Minimum.Y, MathF.Min(c.Y, b.Maximum.Y));
        var z = MathF.Max(b.Minimum.Z, MathF.Min(c.Z, b.Maximum.Z));
        var dx = x - c.X;
        var dy = y - c.Y;
        var dz = z - c.Z;
        return dx * dx + dy * dy + dz * dz <= s.Radius * s.Radius;
    }

    public static bool CullBoundingBox(
        in Vector3 roomCenter,
        in Vector3 roomDimensions,
        in Vector3 viewerPosition,
        in Vector4 zoomRegion,
        in Matrix transform,
        in Vector4[] faceLayoutInfo,
        in Vector4 crossLayoutInfo,
        in BoundingBox box,
        ComputeViewPosition? computeViewPosition = null)
    {
        var half = roomDimensions * 0.5f;
        var roomBounds = new BoundingBox(roomCenter - half, roomCenter + half);
        if (!roomBounds.Intersects(in box))
            return true;

        // 2) test all 8 corners against projector
        //    if any corner projects into visible region, keep the box
        var min = box.Minimum;
        var max = box.Maximum;

        for (int i = 0; i < 8; i++)
        {
            var p = new Vector3(
                (i & 1) != 0 ? max.X : min.X,
                (i & 2) != 0 ? max.Y : min.Y,
                (i & 4) != 0 ? max.Z : min.Z
            );

            if (projectParticleByViewPositionAABB(
                    p,
                    transform,
                    roomCenter,
                    roomDimensions,
                    zoomRegion,
                    viewerPosition,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    faceLayoutInfo,
                    crossLayoutInfo,
                    computeViewPosition))
                return false; // visible
        }

        return true; // culled
    }

    public static bool CullBoundingSphere(
        in Vector3 roomCenter,
        in Vector3 roomDimensions,
        in Vector3 viewerPosition,
        in Vector4 zoomRegion,
        in Matrix transform,
        in Vector4[] faceLayoutInfo,
        in Vector4 crossLayoutInfo,
        in BoundingSphere sphere,
        ComputeViewPosition? computeViewPosition = null)
    {
        var half = roomDimensions * 0.5f;
        var roomBounds = new BoundingBox(roomCenter - half, roomCenter + half);
        if (!SphereIntersectsAabb(sphere, roomBounds))
            return true;

        // Test center first
        if (projectParticleByViewPositionAABB(
                sphere.Center,
                transform,
                roomCenter,
                roomDimensions,
                zoomRegion,
                viewerPosition,
                out _,
                out _,
                out _,
                out _,
                out _,
                faceLayoutInfo,
                crossLayoutInfo,
                computeViewPosition))
            return false; // visible

        // Sample 6 axial directions on the sphere surface
        var r = sphere.Radius;
        var c = sphere.Center;
        var samples = new Vector3[]
        {
            c + new Vector3( r, 0, 0),
            c + new Vector3(-r, 0, 0),
            c + new Vector3( 0, r, 0),
            c + new Vector3( 0,-r, 0),
            c + new Vector3( 0, 0, r),
            c + new Vector3( 0, 0,-r),
        };

        foreach (var p in samples)
        {
            if (projectParticleByViewPositionAABB(
                    p,
                    transform,
                    roomCenter,
                    roomDimensions,
                    zoomRegion,
                    viewerPosition,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    faceLayoutInfo,
                    crossLayoutInfo,
                    computeViewPosition))
                return false; // visible
        }

        return true; // culled
    }
}