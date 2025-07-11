using System;
using Stride.Core.Mathematics;

namespace Fuse.Rendering;

public static class HybridCylindricalCaptureV2
{
    /// <summary>
    ///     Creates a partial-perspective (perspective in X, orthographic in Y)
    ///     ViewProjection matrix for the i-th slice around the Y-axis.
    ///     - numberOfCameras: total slices around 360°
    ///     - cameraIndex: which slice (0..n-1)
    ///     - cylinderHeight: total vertical extent for orthographic coverage in Y
    ///     - nearPlane, farPlane: camera clipping range
    ///     Each slice is oriented so that it faces outward at angle = cameraIndex * (360/n).
    ///     The horizontal FOV = 360°/n, while Y is fixed in [-cylHeight/2, +cylHeight/2].
    /// </summary>
    public static void CreateCylindricalSliceMatrix2(
        int numberOfCameras,
        int cameraIndex,
        float cylinderHeight,
        float nearPlane,
        float farPlane,
        out Matrix view,
        out Matrix projection)
    {
        // 1) Compute the horizontal wedge (in radians)
        var wedgeAngle = MathF.PI * 2f / numberOfCameras;
        var halfWedge = wedgeAngle * 0.5f;

        // 2) The camera i faces outward at centerAngle
        var centerAngle = cameraIndex * wedgeAngle;

        // 3) In the camera's local space (before we rotate), we want:
        //      left  = -nearPlane * tan(halfWedge),
        //      right = +nearPlane * tan(halfWedge).
        //   That ensures the horizontal FOV is wedgeAngle.
        var left = -nearPlane * MathF.Tan(halfWedge);
        var right = +nearPlane * MathF.Tan(halfWedge);

        // 4) Orthographic in Y => top/bottom are fixed at ±(cylHeight/2), 
        //    independent of Z distance
        var top = cylinderHeight * 0.5f;
        var bottom = -top;

        // 5) Build the partial-perspective projection (D3D style, z in [0..1])
        projection = new Matrix
        (
            // M11
            2f * nearPlane / (right - left),
            // M12
            0f,
            // M13
            0f,
            // M14
            0f,

            // M21
            0f,
            // M22
            2f / (top - bottom), // no nearPlane in Y => orthographic
            // M23
            0f,
            // M24
            0f,

            // M31
            (right + left) / (right - left),
            // M32
            (top + bottom) / (top - bottom),
            // M33
            farPlane / (farPlane - nearPlane), // map z to [0..1]
            // M34
            1f, // perspective term goes here

            // M41
            0f,
            // M42
            0f,
            // M43
            -(nearPlane * farPlane) / (farPlane - nearPlane),
            // M44
            0f
        );

        // 6) Build the View matrix so that slice i faces outward at angle "centerAngle"
        //
        // Typically, "View = inverse of the camera transform."
        // If the camera transform is "rotate by centerAngle around Y," 
        // then:
        //    View = RotationY(-centerAngle)
        //
        // But we can also interpret it as "we rotate the world by -centerAngle"
        // so the slice sees +Z in local space.
        view = Matrix.RotationY(-centerAngle);
    }

    /// <summary>
    ///     Creates a partial-perspective slice around the Y-axis (perspective in X, orthographic in Y)
    ///     by defining planes in world space. Adjacent slices share boundary planes for consistent seams.
    ///     This version fixes the issue where nearZ, farZ were assumed after transform, causing
    ///     distortions when the wedge is not 90° or the number of cameras > 4.
    ///     Steps:
    ///     1) We define in world space:
    ///     - left/right planes (radial) at +/- half wedge around the center angle
    ///     - near/far planes parallel to slice's local +Z
    ///     - top/bottom planes at y = ±(cylinderHeight/2)
    ///     2) We build a view matrix that rotates the world by -centerAngle (so the slice faces +Z in camera space).
    ///     3) We transform each plane from world to camera space via planeTransform = transpose(inverse(view)).
    ///     4) We solve for the actual 'zNearCam' by intersecting the near-plane with the camera's local z-axis.
    ///     5) Similarly for 'zFarCam'.
    ///     6) We find the left/right edges by intersecting those planes at z=zNearCam (since it's partial perspective in X).
    ///     7) We keep top/bottom as ±(cylinderHeight/2) in camera Y for orthographic vertical.
    ///     8) Build an off-axis perspective matrix from those computed values.
    ///     9) Multiply by view => final view-projection for that slice.
    /// </summary>
    public static void CreateCylindricalSliceMatrix(
        int numberOfCameras,
        int cameraIndex,
        float cylinderHeight,
        float nearPlaneDistance,
        float farPlaneDistance,
        out Matrix view,
        out Matrix projection)
    {
        //-----------------------------------------------------
        // 1) Basic angles and "view" for the slice
        //-----------------------------------------------------
        var wedgeAngle = MathF.PI * 2f / numberOfCameras; // e.g. 6 cameras => 60°, etc.
        var halfWedge = wedgeAngle * 0.5f;
        var centerAngle = cameraIndex * wedgeAngle;

        // We rotate the world by -centerAngle so that in this camera's space,
        // the slice is looking along +Z.
        view = Matrix.RotationY(-centerAngle);

        //-----------------------------------------------------
        // 2) Define planes in WORLD SPACE
        //-----------------------------------------------------

        // a) Left / Right plane (radial about Y=up axis)
        var leftAngleWorld = centerAngle - halfWedge;
        var rightAngleWorld = centerAngle + halfWedge;

        // Normal points inward to the frustum
        var leftNormal = new Vector3(MathF.Sin(leftAngleWorld), 0, MathF.Cos(leftAngleWorld));
        var rightNormal = new Vector3(MathF.Sin(rightAngleWorld), 0, MathF.Cos(rightAngleWorld));

        var leftPlaneWorld = new Plane(leftNormal, 0f); // goes thru origin
        var rightPlaneWorld = new Plane(rightNormal, 0f); // goes thru origin

        // b) Top / Bottom planes: y = ±(cylinderHeight/2)
        var halfHeight = cylinderHeight * 0.5f;
        // For the top plane, normal is (0,-1,0) if we want the inside to be downward
        // But let's define plane eqn n·X = d => for y= +halfHeight, n=(0,+1,0), d= +halfHeight
        var topPlaneWorld = new Plane(new Vector3(0, 1, 0), halfHeight);
        var bottomPlaneWorld = new Plane(new Vector3(0, -1, 0), halfHeight);

        // c) Near / Far planes
        // The slice's forward direction in WORLD is (sin(centerAngle),0,cos(centerAngle))
        // We place near/far at nearPlaneDistance / farPlaneDistance from origin along that direction.
        var sliceForwardDir = new Vector3(
            MathF.Sin(centerAngle),
            0f,
            MathF.Cos(centerAngle)
        );
        // Plane eqn: n·x = d, with n = sliceForwardDir, d = nearPlaneDistance
        var nearPlaneWorld = new Plane(sliceForwardDir, nearPlaneDistance);
        var farPlaneWorld = new Plane(sliceForwardDir, farPlaneDistance);

        //-----------------------------------------------------
        // 3) Transform these 6 planes into the camera's space
        //-----------------------------------------------------
        var invView = Matrix.Invert(view);
        var invViewT = Matrix.Transpose(invView);

        var nearPlaneCam = TransformPlane(nearPlaneWorld, invViewT);
        var farPlaneCam = TransformPlane(farPlaneWorld, invViewT);
        var leftPlaneCam = TransformPlane(leftPlaneWorld, invViewT);
        var rightPlaneCam = TransformPlane(rightPlaneWorld, invViewT);

        //-----------------------------------------------------
        // 4) Find zNearCam and zFarCam by intersecting the 
        //    near/far planes with the camera's local z-axis (x=0, y=0).
        //-----------------------------------------------------
        var zNearCam = IntersectPlaneAtCameraZ(nearPlaneCam);
        var zFarCam = IntersectPlaneAtCameraZ(farPlaneCam);

        // Make sure near < far, if they're reversed we might want to swap or handle negative.
        if (zNearCam > zFarCam)
        {
            // swap
            var tmp = zNearCam;
            zNearCam = zFarCam;
            zFarCam = tmp;
        }
        // Also ensure zNearCam is positive if your standard camera expects forward = +Z
        // If there's an edge case where nearPlane is behind the camera, you might see zNearCam < 0.
        // That can cause weirdness. Possibly clamp to a small positive value if needed.

        //-----------------------------------------------------
        // 5) For partial perspective in X, we find left/right
        //    by intersecting those planes at z=zNearCam, y=0.
        //-----------------------------------------------------
        var xLeftNear = IntersectPlaneXatZ(leftPlaneCam, zNearCam);
        var xRightNear = IntersectPlaneXatZ(rightPlaneCam, zNearCam);

        //-----------------------------------------------------
        // 6) Orthographic in Y => top/bottom are ±halfHeight 
        //    for the entire range of z.
        //-----------------------------------------------------
        // In camera space, after the plane transform, ideally topPlaneCam 
        // is something like y = +halfHeight at all z. 
        // We'll just define them as numeric constants:
        var yBottom = -halfHeight;
        var yTop = +halfHeight;

        //-----------------------------------------------------
        // 7) Build the off-axis perspective
        //    We'll do a standard D3D-like approach that maps Z in [zNearCam..zFarCam] -> [0..1].
        //-----------------------------------------------------
        projection = CreateOffAxisPerspectiveRH(
            xLeftNear,
            xRightNear,
            yBottom,
            yTop,
            zNearCam,
            zFarCam
        );
    }

    /// <summary>
    ///     Transforms a Plane by (transpose of inverse(M)).
    ///     plane eqn: n·X = d, or A x + B y + C z = D.
    ///     Typically for going from world-space planes to camera-space.
    /// </summary>
    private static Plane TransformPlane(Plane plane, Matrix invT)
    {
        // In Stride, Plane is (Normal, D) with eqn: Normal·X = D.
        // 4D rep: (A, B, C, -D).
        var p4 = new Vector4(plane.Normal, -plane.D);
        var t4 = Vector4.Transform(p4, invT);
        // Rebuild
        var result = new Plane(new Vector3(t4.X, t4.Y, t4.Z), -t4.W);

        // Normalize it (not strictly required but safer):
        var len = result.Normal.Length();
        if (MathF.Abs(len) > 1e-7f)
        {
            result.Normal /= len;
            result.D /= len;
        }

        return result;
    }

    /// <summary>
    ///     Finds the intersection of plane eqn A*x + B*y + C*z = D
    ///     with the camera's local z-axis (x=0, y=0).
    ///     => C*z = D => z = D/C
    /// </summary>
    private static float IntersectPlaneAtCameraZ(Plane planeCam)
    {
        // plane eqn: n.x * x + n.y * y + n.z * z = D
        // for (x=0,y=0) => n.z * z = D => z = D / n.z
        var A = planeCam.Normal.X;
        var B = planeCam.Normal.Y;
        var C = planeCam.Normal.Z;
        var D = planeCam.D;

        // guard if C=0 => no intersection or plane parallel to z-axis
        if (MathF.Abs(C) < 1e-7f)
            // fallback: infinite or something
            return 100000f;
        return D / C;
    }

    /// <summary>
    ///     For plane eqn in camera space: A*x + B*y + C*z = D,
    ///     at y=0, z=fixedZ => A*x + C*fixedZ = D => x = (D - C*fixedZ)/A.
    /// </summary>
    private static float IntersectPlaneXatZ(Plane planeCam, float fixedZ)
    {
        var A = planeCam.Normal.X;
        var B = planeCam.Normal.Y;
        var C = planeCam.Normal.Z;
        var D = planeCam.D;

        // If A ~ 0 => plane is vertical in X? This can cause a big division error if
        // the wedge is exactly 180°, but let's assume numberOfCameras>2 so wedge < 180°.
        return (D - C * fixedZ) / A;
    }

    /// <summary>
    ///     Creates an off-axis RH perspective matrix (D3D style)
    ///     mapping z in [nearZ..farZ] to [0..1].
    ///     row-major fill => typical in Stride for M*(v) usage
    /// </summary>
    private static Matrix CreateOffAxisPerspectiveRH(
        float left, float right,
        float bottom, float top,
        float nearZ, float farZ)
    {
        var twoN = 2f * nearZ;
        var m = new Matrix();

        // row 1
        m.M11 = twoN / (right - left);
        m.M12 = 0f;
        m.M13 = 0f;
        m.M14 = 0f;

        // row 2
        m.M21 = 0f;
        m.M22 = twoN / (top - bottom);
        m.M23 = 0f;
        m.M24 = 0f;

        // row 3
        m.M31 = (right + left) / (right - left);
        m.M32 = (top + bottom) / (top - bottom);
        m.M33 = farZ / (farZ - nearZ); // z -> [0..1]
        m.M34 = 1f; // perspective term

        // row 4
        m.M41 = 0f;
        m.M42 = 0f;
        m.M43 = -nearZ * (farZ / (farZ - nearZ));
        m.M44 = 0f;

        return m;
    }
}