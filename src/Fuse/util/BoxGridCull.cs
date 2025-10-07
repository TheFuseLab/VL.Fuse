using System;
using Stride.Core.Mathematics;

namespace Fuse.util;

/// <summary>
/// CPU-side conservative culling for the Box-Grid projection.
/// Reuses BoxGridProjection helpers (RayPlaneIntersection, IsPointInRectangle, MapToTexture).
/// </summary>
public static class BoxGridCpuCulling
{
    // -------------------------------------------------------------------------
    // Viewer / Transform parameters (mirror of your shader uniforms)
    // -------------------------------------------------------------------------
    public struct ViewerParams
    {
        /// <summary>Anchor for direction (shader: input_622991451)</summary>
        public Vector3 Anchor;

        /// <summary>Axis-wise scale for the normalized direction (shader: input_224581828)</summary>
        public Vector3 ScaleVec;

        /// <summary>Viewer post-transform (shader: input_446715587)</summary>
        public Matrix PostTransform;

        /// <summary>Particle/world point transform (shader: input_3979838717)</summary>
        public Matrix ParticleTransform;

        public static ViewerParams CreateDefault(Vector3 anchor, Vector3 scaleVec, Matrix postTransform, Matrix particleTransform)
        {
            return new ViewerParams
            {
                Anchor = anchor,
                ScaleVec = scaleVec,
                PostTransform = postTransform,
                ParticleTransform = particleTransform
            };
        }
    }

    public struct ZoomRegion
    {
        /// <summary>Min/max in normalized cross-layout UV (0..1)</summary>
        public Vector2 Min;
        public Vector2 Max;
    }

    // -------------------------------------------------------------------------
    // Viewer position (CPU mirror of your VS logic)
    // dir = normalize(P - Anchor); v = dir * ScaleVec; v += Anchor; v -> PostTransform
    // -------------------------------------------------------------------------
    public static Vector3 ComputeViewerPosition(in Vector3 particleWS, in ViewerParams vp)
    {
        var dir = particleWS - vp.Anchor;
        if (dir.LengthSquared() > 1e-12f) dir.Normalize();

        var v = dir * vp.ScaleVec;
        v += vp.Anchor;

        var v4 = new Vector4(v, 1f);
        var vt = Vector4.Transform(v4, vp.PostTransform);
        return new Vector3(vt.X, vt.Y, vt.Z);
    }

    // -------------------------------------------------------------------------
    // Projection test: ray from viewerPS to pointPS against the box faces
    // Uses BoxGridProjection.RayPlaneIntersection, IsPointInRectangle, MapToTexture
    // pointPS and viewerPS are already in the same transformed space
    // -------------------------------------------------------------------------
    public static bool TryProjectToFace(
        in Vector3 pointPS,
        in Vector3 viewerPS,
        in BoxGridProjection.SurfaceData surface,
        in BoxGridProjection.LayoutInfo layout,
        out int hitFaceIndex,
        out Vector2 uv,
        out float viewDistance)
    {
        hitFaceIndex = -1;
        uv = Vector2.Zero;
        viewDistance = 0f;

        var rayDir = pointPS - viewerPS;
        var len2 = rayDir.LengthSquared();
        if (len2 < 1e-20f) return false;
        rayDir /= MathF.Sqrt(len2);

        var roomDims = new Vector3(layout.RoomDimensions.X, layout.RoomDimensions.Y, layout.RoomDimensions.Z);

        var closestDist = float.MaxValue;
        var bestUV = Vector2.Zero;
        var bestFace = -1;

        for (var i = 0; i < 6; i++)
        {
            var planePos = new Vector3(
                surface.PositionsAndType[i].X,
                surface.PositionsAndType[i].Y,
                surface.PositionsAndType[i].Z);

            var planeNrm = new Vector3(
                surface.NormalsAndSize[i].X,
                surface.NormalsAndSize[i].Y,
                surface.NormalsAndSize[i].Z);

            if (!BoxGridProjection.RayPlaneIntersection(viewerPS, rayDir, planePos, planeNrm, out var isect))
                continue;

            if (!BoxGridProjection.IsPointInRectangle(isect, planePos, roomDims, planeNrm))
                continue;

            var distView = (isect - viewerPS).Length();
            if (distView < closestDist)
            {
                closestDist = distView;
                bestFace = i;
                bestUV = BoxGridProjection.MapToTexture(isect, i, layout); // returns 0..1 in 4x3-normalized space
            }
        }

        if (bestFace < 0) return false;

        hitFaceIndex = bestFace;
        uv = bestUV;
        viewDistance = closestDist;
        return true;
    }

    // -------------------------------------------------------------------------
    // AABB → Atlas zoom culling (conservative, sample-based)
    // Applies ParticleTransform to samples and PostTransform to viewer
    // -------------------------------------------------------------------------
    public static bool CullChunkByAtlas(
        in BoundingBox chunkAabbWS,
        in ViewerParams vp,
        in BoxGridProjection.SurfaceData surface,
        in BoxGridProjection.LayoutInfo layout,
        in ZoomRegion zoom,
        float uvEpsilon,
        out byte faceMask)
    {
        faceMask = 0;

        Span<Vector3> samples = stackalloc Vector3[15];
        var sampleCount = GatherSupportPoints(chunkAabbWS, samples);

        var faceMin = new Vector2[6];
        var faceMax = new Vector2[6];
        var faceHas = new bool[6];

        for (var i = 0; i < 6; i++)
        {
            faceMin[i] = new Vector2(float.MaxValue, float.MaxValue);
            faceMax[i] = new Vector2(float.MinValue, float.MinValue);
        }

        for (var s = 0; s < sampleCount; s++)
        {
            var S_ws = samples[s];

            // build viewer (post-transformed) and transform the sample point with ParticleTransform
            var viewerPS = ComputeViewerPosition(S_ws, vp);
            var pointPS  = Vector3.TransformCoordinate(S_ws, vp.ParticleTransform);

            if (!TryProjectToFace(pointPS, viewerPS, surface, layout, out var face, out var uv, out _))
                continue;

            uv = new Vector2(MathUtil.Clamp(uv.X, 0f, 1f), MathUtil.Clamp(uv.Y, 0f, 1f));
            ExpandUvAabb(ref faceMin[face], ref faceMax[face], uv, uvEpsilon);
            faceHas[face] = true;
        }

        var anyOverlap = false;
        for (var f = 0; f < 6; f++)
        {
            if (!faceHas[f]) continue;
            if (AabbOverlap(faceMin[f], faceMax[f], zoom.Min, zoom.Max))
            {
                anyOverlap = true;
                faceMask |= (byte)(1 << f);
            }
        }

        return !anyOverlap; // true -> culled
    }

    // -------------------------------------------------------------------------
    // BoundingSphere → Atlas zoom culling (conservative, sample-based)
    // -------------------------------------------------------------------------
    public static bool CullSphereByAtlas(
        in Vector3 sphereCenterWS,
        float radiusWS,
        in ViewerParams vp,
        in BoxGridProjection.SurfaceData surface,
        in BoxGridProjection.LayoutInfo layout,
        in ZoomRegion zoom,
        float uvEpsilon,
        out byte faceMask)
    {
        faceMask = 0;

        Span<Vector3> samples = stackalloc Vector3[27];
        var sampleCount = GatherSphereSupportPoints(sphereCenterWS, radiusWS, samples);

        var faceMin = new Vector2[6];
        var faceMax = new Vector2[6];
        var faceHas = new bool[6];

        for (var i = 0; i < 6; i++)
        {
            faceMin[i] = new Vector2(float.MaxValue, float.MaxValue);
            faceMax[i] = new Vector2(float.MinValue, float.MinValue);
        }

        for (var s = 0; s < sampleCount; s++)
        {
            var S_ws = samples[s];

            var viewerPS = ComputeViewerPosition(S_ws, vp);
            var pointPS  = Vector3.TransformCoordinate(S_ws, vp.ParticleTransform);

            if (!TryProjectToFace(pointPS, viewerPS, surface, layout, out var face, out var uv, out _))
                continue;

            uv = new Vector2(MathUtil.Clamp(uv.X, 0f, 1f), MathUtil.Clamp(uv.Y, 0f, 1f));
            ExpandUvAabb(ref faceMin[face], ref faceMax[face], uv, uvEpsilon);
            faceHas[face] = true;
        }

        var anyOverlap = false;
        for (var f = 0; f < 6; f++)
        {
            if (!faceHas[f]) continue;
            if (AabbOverlap(faceMin[f], faceMax[f], zoom.Min, zoom.Max))
            {
                anyOverlap = true;
                faceMask |= (byte)(1 << f);
            }
        }

        return !anyOverlap; // true -> culled
    }

    // -------------------------------------------------------------------------
    // Utilities
    // -------------------------------------------------------------------------
    private static int GatherSupportPoints(in BoundingBox aabb, Span<Vector3> dst)
    {
        var min = aabb.Minimum;
        var max = aabb.Maximum;
        var i = 0;

        // 8 corners
        dst[i++] = new Vector3(min.X, min.Y, min.Z);
        dst[i++] = new Vector3(max.X, min.Y, min.Z);
        dst[i++] = new Vector3(min.X, max.Y, min.Z);
        dst[i++] = new Vector3(max.X, max.Y, min.Z);
        dst[i++] = new Vector3(min.X, min.Y, max.Z);
        dst[i++] = new Vector3(max.X, min.Y, max.Z);
        dst[i++] = new Vector3(min.X, max.Y, max.Z);
        dst[i++] = new Vector3(max.X, max.Y, max.Z);

        // 6 face centers
        var c = (min + max) * 0.5f;
        dst[i++] = new Vector3(c.X, min.Y, c.Z);
        dst[i++] = new Vector3(c.X, max.Y, c.Z);
        dst[i++] = new Vector3(min.X, c.Y, c.Z);
        dst[i++] = new Vector3(max.X, c.Y, c.Z);
        dst[i++] = new Vector3(c.X, c.Y, min.Z);
        dst[i++] = new Vector3(c.X, c.Y, max.Z);

        // center
        dst[i++] = c;

        return i;
    }

    private static int GatherSphereSupportPoints(in Vector3 c, float r, Span<Vector3> dst)
    {
        var i = 0;

        // center
        dst[i++] = c;

        // 6 axis extremes
        dst[i++] = c + new Vector3( r, 0, 0);
        dst[i++] = c + new Vector3(-r, 0, 0);
        dst[i++] = c + new Vector3( 0, r, 0);
        dst[i++] = c + new Vector3( 0,-r, 0);
        dst[i++] = c + new Vector3( 0, 0, r);
        dst[i++] = c + new Vector3( 0, 0,-r);

        // 8 cube corners normalized
        const float invSqrt3 = 0.5773502691896258f;
        var corners = new[]{
            new Vector3( 1, 1, 1), new Vector3( 1, 1,-1),
            new Vector3( 1,-1, 1), new Vector3( 1,-1,-1),
            new Vector3(-1, 1, 1), new Vector3(-1, 1,-1),
            new Vector3(-1,-1, 1), new Vector3(-1,-1,-1)
        };
        for (var k = 0; k < 8; k++) dst[i++] = c + corners[k] * (r * invSqrt3);

        // 12 edge midpoints normalized
        var edges = new[]{
            new Vector3( 1, 1, 0), new Vector3( 1,-1, 0), new Vector3(-1, 1, 0), new Vector3(-1,-1, 0),
            new Vector3( 1, 0, 1), new Vector3( 1, 0,-1), new Vector3(-1, 0, 1), new Vector3(-1, 0,-1),
            new Vector3( 0, 1, 1), new Vector3( 0, 1,-1), new Vector3( 0,-1, 1), new Vector3( 0,-1,-1),
        };
        for (var k = 0; k < 12; k++) { var d = edges[k]; d.Normalize(); dst[i++] = c + d * r; }

        return i; // 27
    }

    private static void ExpandUvAabb(ref Vector2 min, ref Vector2 max, in Vector2 p, float eps)
    {
        min.X = MathF.Min(min.X, p.X - eps);
        min.Y = MathF.Min(min.Y, p.Y - eps);
        max.X = MathF.Max(max.X, p.X + eps);
        max.Y = MathF.Max(max.Y, p.Y + eps);

        min.X = MathUtil.Clamp(min.X, 0f, 1f);
        min.Y = MathUtil.Clamp(min.Y, 0f, 1f);
        max.X = MathUtil.Clamp(max.X, 0f, 1f);
        max.Y = MathUtil.Clamp(max.Y, 0f, 1f);
    }

    private static bool AabbOverlap(in Vector2 aMin, in Vector2 aMax, in Vector2 bMin, in Vector2 bMax)
    {
        if (aMax.X < bMin.X || aMin.X > bMax.X) return false;
        if (aMax.Y < bMin.Y || aMin.Y > bMax.Y) return false;
        return true;
    }
}
