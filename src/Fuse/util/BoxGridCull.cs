using System;
using Stride.Core.Mathematics;

namespace Fuse.util;

public static class BoxgridCpuProjector
{
    /// <summary>
    ///     CPU equivalent of projectParticleByViewPositionAABB + mapToTexture.
    ///     Returns false if ray from ViewerPosition through particlePos does not hit the room or visible atlas region.
    /// </summary>
    public static bool TryProjectPointToAtlas(
        in BoxgridProjector proj,
        in Vector3 particlePos,
        out int faceIndex,
        out Vector2 atlasPos4x3, // in 4x3 layout space AFTER zoom (like uvPack.xy on GPU)
        out float offsetDist // = distance(intersection, particlePos), like uvPack.z on GPU
    )
    {
        faceIndex = -1;
        atlasPos4x3 = Vector2.Zero;
        offsetDist = 0f;

        var ro = proj.ViewerPosition;
        var dir = particlePos - ro;
        var lenSq = dir.LengthSquared();
        if (lenSq <= float.Epsilon)
            return false;

        var rd = dir / (float)System.Math.Sqrt(lenSq);

        var bounds = proj.RoomBounds;
        var boxMin = bounds.Minimum;
        var boxMax = bounds.Maximum;

        // --- slab intersection ---
        var invDir = new Vector3(
            1f / rd.X,
            1f / rd.Y,
            1f / rd.Z
        );

        var t0 = (boxMin - ro) * invDir;
        var t1 = (boxMax - ro) * invDir;

        var tmin = Vector3.Min(t0, t1);
        var tmax = Vector3.Max(t0, t1);

        var tEnter = System.Math.Max(System.Math.Max(tmin.X, tmin.Y), tmin.Z);
        var tExit = System.Math.Min(System.Math.Min(tmax.X, tmax.Y), tmax.Z);

        if (tExit < 0f || tEnter > tExit)
            return false;

        var t = tEnter > 0f ? tEnter : tExit;
        if (t <= 0f)
            return false;

        var hit = ro + rd * t;

        // --- classify face like GPU indices ---
        const float eps = 1e-4f;

        if (System.Math.Abs(hit.Y - boxMin.Y) < eps) faceIndex = 0; // floor
        else if (System.Math.Abs(hit.Y - boxMax.Y) < eps) faceIndex = 1; // ceiling
        else if (System.Math.Abs(hit.X - boxMax.X) < eps) faceIndex = 2; // right
        else if (System.Math.Abs(hit.Z - boxMax.Z) < eps) faceIndex = 3; // back
        else if (System.Math.Abs(hit.X - boxMin.X) < eps) faceIndex = 4; // left
        else if (System.Math.Abs(hit.Z - boxMin.Z) < eps) faceIndex = 5; // front
        else
            return false;

        // --- per-face UV in [0,1], mirroring HLSL mapToTexture ---
        var size = proj.RoomSize;
        float u = 0f, v = 0f;

        switch (faceIndex)
        {
            case 0: // Floor
                u = (hit.X - boxMin.X) / size.X;
                v = 1f - (hit.Z - boxMin.Z) / size.Z;
                break;
            case 1: // Ceiling
                u = (hit.X - boxMin.X) / size.X;
                v = (hit.Z - boxMin.Z) / size.Z;
                break;
            case 2: // Right wall
                u = (hit.Z - boxMin.Z) / size.Z;
                v = (hit.Y - boxMin.Y) / size.Y;
                break;
            case 3: // Back wall
                u = 1f - (hit.X - boxMin.X) / size.X;
                v = (hit.Y - boxMin.Y) / size.Y;
                break;
            case 4: // Left wall
                u = 1f - (hit.Z - boxMin.Z) / size.Z;
                v = (hit.Y - boxMin.Y) / size.Y;
                break;
            case 5: // Front wall
                u = (hit.X - boxMin.X) / size.X;
                v = (hit.Y - boxMin.Y) / size.Y;
                break;
            default:
                return false;
        }

        // strict bounds check (no saturate)
        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return false;

        // --- atlas mapping (exactly like HLSL mapToTexture) ---

        // 1) map face-local UV -> 4x3 layout
        // faceLayout: (x, y, w, h) in 4x3 space
        var layout = proj.FaceLayoutInfo[faceIndex];
        var texX = layout.X + u * layout.Z;
        var texY = layout.Y + v * layout.W;

        // 2) normalize to [0,1] atlas
        var uvNorm = new Vector2(texX / 4f, texY / 3f);

        // 3) apply zoomRegion in normalized space
        var zoomMin = new Vector2(proj.ZoomRegion.X, proj.ZoomRegion.Y);
        var zoomMax = new Vector2(proj.ZoomRegion.Z, proj.ZoomRegion.W);
        var zoomSize = zoomMax - zoomMin;

        // avoid div-by-zero if misconfigured
        if (zoomSize.X <= 0f || zoomSize.Y <= 0f)
            return false;

        var uvZoomedNorm = (uvNorm - zoomMin) / zoomSize;

        // 4) scale back to 4x3 layout space (this is what your shader returns in uvPack.xy)
        atlasPos4x3 = new Vector2(uvZoomedNorm.X * 4f, uvZoomedNorm.Y * 3f);

        // z-component: match GPU = distance(intersection, particlePos)
        offsetDist = Vector3.Distance(hit, particlePos);

        // Optional: require zoomed UV inside [0,1] to be "visible"
        if (uvZoomedNorm.X < 0f || uvZoomedNorm.X > 1f ||
            uvZoomedNorm.Y < 0f || uvZoomedNorm.Y > 1f)
            return false;

        return true;
    }
    
    public static bool CullBoundingBox(in BoxgridProjector proj, in BoundingBox box)
    {
        // 1) quick reject vs room
        if (!box.Intersects(proj.RoomBounds))
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

            if (BoxgridCpuProjector.TryProjectPointToAtlas(proj, p,
                    out _, out _, out _))
                return false; // visible
        }

        return true; // culled
    }


    public static bool CullBoundingSphere(in BoxgridProjector proj, in BoundingSphere sphere)
    {
        // 1) quick reject vs room AABB
        if (!SphereIntersectsAabb(sphere, proj.RoomBounds))
            return true;

        // 2) conservative test: if center projects into visible region -> keep
        if (TryProjectPointToAtlas(proj, sphere.Center,
                out _, out _, out _))
            return false;

        // Optional: sample a few directions around center for tighter cull.
        // For safety in your use-case, I'd keep clusters if in doubt:
        return false;
    }

    private static bool SphereIntersectsAabb(in BoundingSphere s, in BoundingBox b)
    {
        // classic closest-point test
        var c = s.Center;
        var x = MathF.Max(b.Minimum.X, MathF.Min(c.X, b.Maximum.X));
        var y = MathF.Max(b.Minimum.Y, MathF.Min(c.Y, b.Maximum.Y));
        var z = MathF.Max(b.Minimum.Z, MathF.Min(c.Z, b.Maximum.Z));

        var dx = x - c.X;
        var dy = y - c.Y;
        var dz = z - c.Z;

        return dx * dx + dy * dy + dz * dz <= s.Radius * s.Radius;
    }
    
    public static BoxgridProjector CreateBoxgridProjector(in BoundingBox roomBounds, in Vector3 viewerPosition, in Vector4[] faceLayoutInfo)
    {
        return new BoxgridProjector
        {
            RoomCenter = roomBounds.Center,
            RoomSize = roomBounds.Maximum - roomBounds.Minimum,
            ViewerPosition = viewerPosition,
            FaceLayoutInfo = faceLayoutInfo,
        };
    }

    public struct BoxgridProjector
    {
        public Vector3 RoomCenter; // room center in world space
        public Vector3 RoomSize; // full size (width, height, depth)
        public Vector4 ZoomRegion; // (minU, minV, maxU, maxV) in normalized atlas space
        public Vector3 ViewerPosition; // world space

        // length 6, each: (x, y, w, h) in 4x3 layout space, same as GPU faceLayoutInfo
        public Vector4[] FaceLayoutInfo;

        public BoundingBox RoomBounds
        {
            get
            {
                var half = RoomSize * 0.5f;
                return new BoundingBox(RoomCenter - half, RoomCenter + half);
            }
        }
    }
}