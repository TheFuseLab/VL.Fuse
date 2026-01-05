// ShadowMapMath.cs
// Minimal public API:
//   - ShadowMapMath.DirectionalShadowSetup
//   - ShadowMapMath.BuildDirectionalShadowFromViewProjection(...)

using System;
using Stride.Core.Mathematics;

public static class ShadowMapMath
{
    /// <summary>
    ///     Build a stable single-map directional shadow configuration from camera View+Projection matrices.
    ///     Assumptions / conventions:
    ///     - lightDirWorld points TOWARDS the light source (opposite of ray travel).
    ///     - Row-vector convention for matrices: clip = mul(float4(posWS,1), M).
    ///     Parameters:
    ///     - sliceNearDist / sliceFarDist: distances along camera forward (world units) defining the region you want shadowed.
    ///     - viewForwardAxisInViewSpace: choose (0,0, 1) if your view-space forward is +Z, or (0,0,-1) if forward is -Z.
    ///     - useRightHandedOrtho: toggle only if your later depth convention expects RH-style z mapping.
    /// </summary>
    public static DirectionalShadowSetup BuildDirectionalShadowFromViewProjection(
        Vector3 lightDirWorld,
        Matrix cameraView,
        Matrix cameraProjection,
        float sliceNearDist,
        float sliceFarDist,
        int shadowResolution,
        Vector3 viewForwardAxisInViewSpace,
        float marginXY = 10f,
        float marginZ = 50f,
        bool makeSquareXY = true,
        bool snapToTexels = true,
        bool useRightHandedOrtho = false)
    {
        // === Input Validation ===
        if (sliceNearDist >= sliceFarDist)
            throw new ArgumentException("sliceNearDist must be < sliceFarDist");

        if (sliceNearDist < 0f)
            throw new ArgumentException("sliceNearDist must be >= 0");

        if (shadowResolution <= 0)
            throw new ArgumentException("shadowResolution must be > 0");

        if (lightDirWorld.LengthSquared() < 1e-8f)
            throw new ArgumentException("lightDirWorld cannot be zero");

        if (viewForwardAxisInViewSpace.LengthSquared() < 1e-8f)
            throw new ArgumentException("viewForwardAxisInViewSpace cannot be zero");

        // Normalize light direction
        lightDirWorld.Normalize();

        // 1) Extract camera basis from View (robust: invert then transform canonical axes)
        var cam = ExtractCameraBasisFromViewMatrix(cameraView, viewForwardAxisInViewSpace);

        // 2) Extract FOV + aspect from Projection (standard perspective matrices)
        if (!TryExtractPerspectiveFovYAndAspect(cameraProjection, out var fovYRadians, out var aspect))
        {
            // Fallback: should not happen for a real perspective projection.
            fovYRadians = MathF.PI / 3f; // 60°
            aspect = 16f / 9f;
        }

        // 3) Build shadow from frustum slice
        return BuildDirectionalShadowFromFrustumSlice_Internal(
            lightDirWorld,
            cam,
            fovYRadians,
            aspect,
            sliceNearDist,
            sliceFarDist,
            shadowResolution,
            marginXY,
            marginZ,
            makeSquareXY,
            snapToTexels,
            useRightHandedOrtho
        );
    }

    // =========================
    // INTERNAL BUILD PIPELINE
    // =========================

    private static DirectionalShadowSetup BuildDirectionalShadowFromFrustumSlice_Internal(
        Vector3 lightDirWS,
        CameraBasis camera,
        float fovYRadians,
        float aspect,
        float sliceNearDist,
        float sliceFarDist,
        int shadowResolution,
        float marginXY,
        float marginZ,
        bool makeSquareXY,
        bool snapToTexels,
        bool useRightHandedOrtho)
    {
        // A) Frustum slice corners in world space
        var cornersWS = GetFrustumSliceCornersWS(camera, fovYRadians, aspect, sliceNearDist, sliceFarDist);

        // B) Anchor (stable placement): center of slice corners
        var anchorWS = ComputeAverage(cornersWS);

        // C) Light view matrix (World -> LightView)
        var lightView = BuildDirectionalLightView(lightDirWS, anchorWS);

        // D) Bounds in light space
        var boundsLS = ComputeAabbInSpace(lightView, cornersWS);
        boundsLS = ExpandAabb(boundsLS, marginXY, marginZ);

        if (makeSquareXY)
            boundsLS = MakeAabbSquareXY(boundsLS);

        if (snapToTexels)
            boundsLS = SnapAabbXYToTexelGrid(boundsLS, shadowResolution);

        // E) Ortho projection (LightView -> LightClip)
        var min = boundsLS.Minimum;
        var max = boundsLS.Maximum;

        var lightProj = useRightHandedOrtho
            ? OrthoOffCenterRH_Row(min.X, max.X, min.Y, max.Y, min.Z, max.Z)
            : OrthoOffCenterLH_Row(min.X, max.X, min.Y, max.Y, min.Z, max.Z);

        // F) Combined matrix (row-vector chain: world * View * Proj)
        var lightViewProj = lightView * lightProj;

        // G) Useful shader params
        var size = max - min;
        var shadowTexelSize = new Vector2(1f / shadowResolution, 1f / shadowResolution);
        var worldUnitsPerTexelLS = new Vector2(size.X / shadowResolution, size.Y / shadowResolution);

        // Bias heuristics
        var depthUnitsPerTexel = size.Z / shadowResolution;
        var recommendedConstantBias = 2.0f * depthUnitsPerTexel;
        var recommendedSlopeScale = 1.5f;

        return new DirectionalShadowSetup
        {
            LightView = lightView,
            LightProj = lightProj,
            LightViewProj = lightViewProj,

            BoundsLightSpace = boundsLS,

            AnchorWorld = anchorWS,
            LightDirWorld = lightDirWS,
            ShadowResolution = shadowResolution,

            ShadowTexelSize = shadowTexelSize,
            WorldUnitsPerTexelLight = worldUnitsPerTexelLS,
            RecommendedConstantBias = recommendedConstantBias,
            RecommendedSlopeScale = recommendedSlopeScale
        };
    }

    // =========================
    // CAMERA EXTRACTION
    // =========================

    private static CameraBasis ExtractCameraBasisFromViewMatrix(Matrix view, Vector3 viewForwardAxisVS)
    {
        Matrix invView;
        Matrix.Invert(ref view, out invView);

        // For Row-Major: Position is in Row 4 of the inverse matrix
        var posWS = new Vector3(invView.M41, invView.M42, invView.M43);

        // Extract basis vectors from inverse view matrix
        var rightWS = NormalizeSafe(new Vector3(invView.M11, invView.M12, invView.M13), Vector3.UnitX);
        var upWS = NormalizeSafe(new Vector3(invView.M21, invView.M22, invView.M23), Vector3.UnitY);

        // Correctly transform the forward axis through the inverse view matrix
        var fwdWS = NormalizeSafe(TransformDirectionRow(viewForwardAxisVS, invView), -Vector3.UnitZ);

        return new CameraBasis
        {
            PositionWS = posWS,
            RightWS = rightWS,
            UpWS = upWS,
            ForwardWS = fwdWS
        };
    }

    private static bool TryExtractPerspectiveFovYAndAspect(Matrix projection, out float fovYRadians, out float aspect)
    {
        var m11 = projection.M11;
        var m22 = projection.M22;

        if (MathF.Abs(m11) < 1e-8f || MathF.Abs(m22) < 1e-8f)
        {
            fovYRadians = 0f;
            aspect = 1f;
            return false;
        }

        // Use Abs to handle flipped projection matrices (negative M22)
        fovYRadians = 2f * MathF.Atan(1f / MathF.Abs(m22));
        aspect = MathF.Abs(m22 / m11);
        return true;
    }

    // =========================
    // FRUSTUM SLICE CORNERS
    // =========================

    private static Vector3[] GetFrustumSliceCornersWS(
        CameraBasis cam,
        float fovYRadians,
        float aspect,
        float nearDist,
        float farDist)
    {
        var tanHalfFovY = MathF.Tan(0.5f * fovYRadians);

        var nearHalfH = nearDist * tanHalfFovY;
        var nearHalfW = nearHalfH * aspect;

        var farHalfH = farDist * tanHalfFovY;
        var farHalfW = farHalfH * aspect;

        var nc = cam.PositionWS + cam.ForwardWS * nearDist;
        var fc = cam.PositionWS + cam.ForwardWS * farDist;

        var up = cam.UpWS;
        var right = cam.RightWS;

        // near plane (lt, rt, rb, lb)
        var nlt = nc + up * nearHalfH - right * nearHalfW;
        var nrt = nc + up * nearHalfH + right * nearHalfW;
        var nrb = nc - up * nearHalfH + right * nearHalfW;
        var nlb = nc - up * nearHalfH - right * nearHalfW;

        // far plane (lt, rt, rb, lb)
        var flt = fc + up * farHalfH - right * farHalfW;
        var frt = fc + up * farHalfH + right * farHalfW;
        var frb = fc - up * farHalfH + right * farHalfW;
        var flb = fc - up * farHalfH - right * farHalfW;

        return new[] { nlt, nrt, nrb, nlb, flt, frt, frb, flb };
    }

    private static Vector3 ComputeAverage(Vector3[] points)
    {
        var sum = Vector3.Zero;
        for (var i = 0; i < points.Length; i++)
            sum += points[i];
        return sum / points.Length;
    }

    // =========================
    // LIGHT VIEW (DIRECTIONAL)
    // =========================

    private static Matrix BuildDirectionalLightView(Vector3 lightDirWS, Vector3 anchorWS)
    {
        // lightDirWS points TOWARDS the light source; rays travel along -lightDirWS
        var forwardWS = NormalizeSafe(-lightDirWS, -Vector3.UnitZ);

        BuildBasisFromForward(forwardWS, out var rightWS, out var upWS);
        return BuildViewMatrixRow(anchorWS, rightWS, upWS, forwardWS);
    }

    private static void BuildBasisFromForward(Vector3 forwardWS, out Vector3 rightWS, out Vector3 upWS)
    {
        forwardWS = NormalizeSafe(forwardWS, -Vector3.UnitZ);

        var upHint = ChooseStableUp(forwardWS);

        // LH coordinate system: right = up × forward
        rightWS = NormalizeSafe(Vector3.Cross(upHint, forwardWS), Vector3.UnitX);

        // up = forward × right (ensures orthonormal basis)
        upWS = Vector3.Cross(forwardWS, rightWS);
    }

    private static Vector3 ChooseStableUp(Vector3 dirWS)
    {
        var worldUp = Vector3.UnitY;
        return MathF.Abs(Vector3.Dot(dirWS, worldUp)) > 0.99f ? Vector3.UnitZ : worldUp;
    }

    private static Matrix BuildViewMatrixRow(Vector3 originWS, Vector3 rightWS, Vector3 upWS, Vector3 forwardWS)
    {
        // Row-major for row-vector multiplication:
        // v_view = mul(v_world, View)
        return new Matrix(
            rightWS.X, upWS.X, forwardWS.X, 0f,
            rightWS.Y, upWS.Y, forwardWS.Y, 0f,
            rightWS.Z, upWS.Z, forwardWS.Z, 0f,
            -Vector3.Dot(rightWS, originWS),
            -Vector3.Dot(upWS, originWS),
            -Vector3.Dot(forwardWS, originWS),
            1f
        );
    }

    // =========================
    // BOUNDS IN LIGHT SPACE
    // =========================

    private static BoundingBox ComputeAabbInSpace(Matrix worldToSpace, Vector3[] pointsWS)
    {
        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (var i = 0; i < pointsWS.Length; i++)
        {
            var p = TransformPointRow(pointsWS[i], worldToSpace);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        return new BoundingBox(min, max);
    }

    private static BoundingBox ExpandAabb(BoundingBox b, float marginXY, float marginZ)
    {
        var min = b.Minimum;
        var max = b.Maximum;

        min.X -= marginXY;
        min.Y -= marginXY;
        min.Z -= marginZ;
        max.X += marginXY;
        max.Y += marginXY;
        max.Z += marginZ;

        return new BoundingBox(min, max);
    }

    private static BoundingBox MakeAabbSquareXY(BoundingBox b)
    {
        var min = b.Minimum;
        var max = b.Maximum;

        var center = 0.5f * (min + max);
        var size = max - min;

        var half = 0.5f * MathF.Max(size.X, size.Y);

        min.X = center.X - half;
        max.X = center.X + half;
        min.Y = center.Y - half;
        max.Y = center.Y + half;

        return new BoundingBox(min, max);
    }

    private static BoundingBox SnapAabbXYToTexelGrid(BoundingBox b, int shadowResolution)
    {
        var min = b.Minimum;
        var max = b.Maximum;

        var size = max - min;
        var texelX = size.X / shadowResolution;
        var texelY = size.Y / shadowResolution;

        // Snap min directly for better numerical stability
        min.X = MathF.Floor(min.X / texelX) * texelX;
        min.Y = MathF.Floor(min.Y / texelY) * texelY;
        max.X = min.X + size.X;
        max.Y = min.Y + size.Y;

        return new BoundingBox(min, max);
    }

    // =========================
    // ORTHO PROJECTION (ROW)
    // =========================

    // LH: +Z forward, z in [0..1]
    private static Matrix OrthoOffCenterLH_Row(float left, float right, float bottom, float top, float zNear, float zFar)
    {
        var invW = 1f / (right - left);
        var invH = 1f / (top - bottom);
        var invD = 1f / (zFar - zNear);

        return new Matrix(
            2f * invW, 0f, 0f, 0f,
            0f, 2f * invH, 0f, 0f,
            0f, 0f, 1f * invD, 0f,
            (left + right) * -invW,
            (top + bottom) * -invH,
            -zNear * invD,
            1f
        );
    }

    // RH: typically -Z forward, z in [0..1] with swapped depth mapping
    private static Matrix OrthoOffCenterRH_Row(float left, float right, float bottom, float top, float zNear, float zFar)
    {
        var invW = 1f / (right - left);
        var invH = 1f / (top - bottom);
        var invD = 1f / (zNear - zFar);

        return new Matrix(
            2f * invW, 0f, 0f, 0f,
            0f, 2f * invH, 0f, 0f,
            0f, 0f, 1f * invD, 0f,
            (left + right) * -invW,
            (top + bottom) * -invH,
            zNear * invD,
            1f
        );
    }

    // =========================
    // MATRIX/VECTOR OPS (ROW)
    // =========================

    private static Vector3 TransformPointRow(Vector3 pWS, Matrix m)
    {
        // p' = mul(float4(p,1), m)
        var x = pWS.X * m.M11 + pWS.Y * m.M21 + pWS.Z * m.M31 + m.M41;
        var y = pWS.X * m.M12 + pWS.Y * m.M22 + pWS.Z * m.M32 + m.M42;
        var z = pWS.X * m.M13 + pWS.Y * m.M23 + pWS.Z * m.M33 + m.M43;
        return new Vector3(x, y, z);
    }

    private static Vector3 TransformDirectionRow(Vector3 d, Matrix m)
    {
        // d' = mul(float4(d,0), m)
        var x = d.X * m.M11 + d.Y * m.M21 + d.Z * m.M31;
        var y = d.X * m.M12 + d.Y * m.M22 + d.Z * m.M32;
        var z = d.X * m.M13 + d.Y * m.M23 + d.Z * m.M33;
        return new Vector3(x, y, z);
    }

    private static Vector3 NormalizeSafe(Vector3 v, Vector3 fallback)
    {
        var lenSq = v.LengthSquared();
        if (lenSq < 1e-12f)
            return fallback;
        return v / MathF.Sqrt(lenSq);
    }

    // =========================
    // PUBLIC API
    // =========================

    public struct DirectionalShadowSetup
    {
        // Matrices (row-vector convention):
        //   clip = mul(float4(posWS,1), LightViewProj)
        public Matrix LightView;          // World -> LightView
        public Matrix LightProj;          // LightView -> LightClip
        public Matrix LightViewProj;      // World -> LightClip (View * Proj)

        // Light-space coverage used to build the ortho
        public BoundingBox BoundsLightSpace;

        // Convenience
        public Vector3 AnchorWorld;       // Center used for light view placement
        public Vector3 LightDirWorld;     // Input light direction (towards light source)
        public int ShadowResolution;      // N (assuming square map)

        // Params you typically pass to shaders later
        public Vector2 ShadowTexelSize;           // (1/N, 1/N)
        public Vector2 WorldUnitsPerTexelLight;   // (boundsSizeXY / N) in light space
        public float RecommendedConstantBias;     // Constant depth bias
        public float RecommendedSlopeScale;       // Slope-scale bias factor
    }

    // =========================
    // INTERNAL TYPES
    // =========================

    private struct CameraBasis
    {
        public Vector3 PositionWS;
        public Vector3 RightWS;
        public Vector3 UpWS;
        public Vector3 ForwardWS;
    }
}