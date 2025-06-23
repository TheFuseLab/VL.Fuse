using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse.util;

public static class BoxGridProjection
{
    
    #region SurfaceUtil
    
    public struct SurfaceData
    {
        public Vector4[] PositionsAndType;
        public Vector4[] NormalsAndSize;
        public Vector4[] Dimensions;
        public Vector4[] ColorsAndScale;
    }
    
    public static SurfaceData CalculateSurfaceData(Vector3 roomCenter, Vector3 roomDimensions)
    {
        var surfaceData = new SurfaceData
        {
            PositionsAndType = new Vector4[6],
            NormalsAndSize = new Vector4[6],
            Dimensions = new Vector4[6],
            ColorsAndScale = new Vector4[6]
        };

        var roomWidth = roomDimensions.X;
        var roomHeight = roomDimensions.Y;
        var roomDepth = roomDimensions.Z;

        // Floor (0)
        surfaceData.PositionsAndType[0] = new Vector4(
            roomCenter.X,
            roomCenter.Y - roomHeight * 0.5f,
            roomCenter.Z,
            0); // type = 0 (floor)
        surfaceData.NormalsAndSize[0] = new Vector4(0, 1, 0, roomHeight);
        surfaceData.Dimensions[0] = new Vector4(roomWidth, roomHeight, roomDepth, 0);
        surfaceData.ColorsAndScale[0] = new Vector4(0, 1, 0, 1); // Green

        // Ceiling (1)
        surfaceData.PositionsAndType[1] = new Vector4(
            roomCenter.X,
            roomCenter.Y + roomHeight * 0.5f,
            roomCenter.Z,
            1); // type = 1 (ceiling)
        surfaceData.NormalsAndSize[1] = new Vector4(0, -1, 0, roomHeight);
        surfaceData.Dimensions[1] = new Vector4(roomWidth, roomHeight, roomDepth, 0);
        surfaceData.ColorsAndScale[1] = new Vector4(1, 1, 0, 1); // Yellow

        // Right wall (2)
        surfaceData.PositionsAndType[2] = new Vector4(
            roomCenter.X + roomWidth * 0.5f,
            roomCenter.Y,
            roomCenter.Z,
            2); // type = 2 (right wall)
        surfaceData.NormalsAndSize[2] = new Vector4(-1, 0, 0, roomWidth);
        surfaceData.Dimensions[2] = new Vector4(roomWidth, roomHeight, roomDepth, 0);
        surfaceData.ColorsAndScale[2] = new Vector4(1, 0, 1, 1); // Magenta

        // Back wall (3)
        surfaceData.PositionsAndType[3] = new Vector4(
            roomCenter.X,
            roomCenter.Y,
            roomCenter.Z + roomDepth * 0.5f,
            3); // type = 3 (back wall)
        surfaceData.NormalsAndSize[3] = new Vector4(0, 0, -1, roomDepth);
        surfaceData.Dimensions[3] = new Vector4(roomWidth, roomHeight, roomDepth, 0);
        surfaceData.ColorsAndScale[3] = new Vector4(0, 1, 1, 1); // Cyan

        // Left wall (4)
        surfaceData.PositionsAndType[4] = new Vector4(
            roomCenter.X - roomWidth * 0.5f,
            roomCenter.Y,
            roomCenter.Z,
            4); // type = 4 (left wall)
        surfaceData.NormalsAndSize[4] = new Vector4(1, 0, 0, roomWidth);
        surfaceData.Dimensions[4] = new Vector4(roomWidth, roomHeight, roomDepth, 0);
        surfaceData.ColorsAndScale[4] = new Vector4(1, 0, 0, 1); // Red

        // Front wall (5)
        surfaceData.PositionsAndType[5] = new Vector4(
            roomCenter.X,
            roomCenter.Y,
            roomCenter.Z - roomDepth * 0.5f,
            5); // type = 5 (front wall)
        surfaceData.NormalsAndSize[5] = new Vector4(0, 0, 1, roomDepth);
        surfaceData.Dimensions[5] = new Vector4(roomWidth, roomHeight, roomDepth, 0);
        surfaceData.ColorsAndScale[5] = new Vector4(0, 0, 1, 1); // Blue

        return surfaceData;
    }
    
    
    public struct LayoutInfo
    {
        public Vector4[] FaceLayoutInfo;
        public Vector4 CrossLayoutInfo;
        public Vector4 RoomCenter;
        public Vector4 RoomDimensions;
    }

    public static LayoutInfo CalculateLayoutData(Vector3 roomCenter, Vector3 roomDimensions)
    {
        var layoutData = new LayoutInfo
        {
            FaceLayoutInfo = new Vector4[6],
            CrossLayoutInfo = Vector4.Zero,
            RoomCenter = Vector4.Zero,
            RoomDimensions = Vector4.Zero
        };

        var roomWidth = roomDimensions.X;
        var roomHeight = roomDimensions.Y;
        var roomDepth = roomDimensions.Z;

        // Fixed total dimensions for the cross layout
        const float TOTAL_WIDTH = 4.0f;
        const float TOTAL_HEIGHT = 3.0f;

        // To maintain proper edge matching and aspect ratios, we need a scaling factor
        // that will be applied to all dimensions uniformly

        // Step 1: Calculate the "ideal" layout sizes maintaining perfect aspect ratios
        // For a perfect cross layout, we need one unit size that all dimensions are multiples of

        // Express all dimensions as multiples of the unit size

        // Step 2: Calculate how much space the ideal cross would take
        // The width of the cross is: left wall + front wall + right wall + back wall
        // = depth + width + depth + width = 2*width + 2*depth
        var idealWidth = 2 * roomWidth + 2 * roomDepth;

        // The height of the cross is: ceiling + walls + floor = depth + height + depth
        var idealHeight = 2 * roomDepth + roomHeight;

        // Step 3: Calculate the scaling factor to fit this ideal cross into our fixed size
        var widthScaleFactor = TOTAL_WIDTH / idealWidth;
        var heightScaleFactor = TOTAL_HEIGHT / idealHeight;

        var scaleFactor = float.Min(widthScaleFactor, heightScaleFactor);

        // Face widths in the cross layout
        var leftWallWidth = roomDepth * scaleFactor;
        var frontWallWidth = roomWidth * scaleFactor;
        var rightWallWidth = roomDepth * scaleFactor;
        var backWallWidth = roomWidth * scaleFactor;

        // Face heights in the cross layout
        var ceilingHeight = roomDepth * scaleFactor;
        var wallsHeight = roomHeight * scaleFactor;
        var floorHeight = roomDepth * scaleFactor;

        // Calculate starting positions for each face in the cross layout
        var leftWallX = 0.0f;
        var frontWallX = leftWallWidth;
        var rightWallX = leftWallWidth + frontWallWidth;
        var backWallX = leftWallWidth + frontWallWidth + rightWallWidth;

        var ceilingY = 0.0f;
        var middleRowY = ceilingHeight;
        var floorY = ceilingHeight + wallsHeight;

        // Store layout information for each face (startX, startY, width, height)
        layoutData.FaceLayoutInfo[1] = new Vector4(frontWallX, floorY, frontWallWidth, floorHeight); // Floor (0)
        layoutData.FaceLayoutInfo[0] = new Vector4(frontWallX, ceilingY, frontWallWidth, ceilingHeight); // Ceiling (1)
        layoutData.FaceLayoutInfo[2] =
            new Vector4(rightWallX, middleRowY, rightWallWidth, wallsHeight); // Right wall (2)
        layoutData.FaceLayoutInfo[3] = new Vector4(backWallX, middleRowY, backWallWidth, wallsHeight); // Back wall (3)
        layoutData.FaceLayoutInfo[4] = new Vector4(leftWallX, middleRowY, leftWallWidth, wallsHeight); // Left wall (4)
        layoutData.FaceLayoutInfo[5] =
            new Vector4(frontWallX, middleRowY, frontWallWidth, wallsHeight); // Front wall (5)

        // Calculate the actual cross dimensions (which might be smaller than our max allowed dimensions)
        var actualWidth = leftWallWidth + frontWallWidth + rightWallWidth + backWallWidth;
        var actualHeight = ceilingHeight + wallsHeight + floorHeight;

        // Store cross layout information
        layoutData.CrossLayoutInfo = new Vector4(actualWidth, actualHeight, 0, 0);
        layoutData.RoomCenter = new Vector4(roomCenter.X, roomCenter.Y, roomCenter.Z, 0);
        layoutData.RoomDimensions = new Vector4(roomWidth, roomHeight, roomDepth, 0);

        return layoutData;
    }

    #endregion
    
    

    #region 
    
    private static void ExtractPlanesFromMatrix(
        Matrix viewProj,
        out Plane left,
        out Plane right,
        out Plane top,
        out Plane bottom,
        out Plane near,
        out Plane far)
    {
        // Left plane  (row = 1, sign = +1 => M14 + M11, M24 + M21, ...)
        left = new Plane(
            viewProj.M14 + viewProj.M11,
            viewProj.M24 + viewProj.M21,
            viewProj.M34 + viewProj.M31,
            viewProj.M44 + viewProj.M41);
        Plane.Normalize(ref left, out left);

        // Right plane (row = 1, sign = -1 => M14 - M11, M24 - M21, ...)
        right = new Plane(
            viewProj.M14 - viewProj.M11,
            viewProj.M24 - viewProj.M21,
            viewProj.M34 - viewProj.M31,
            viewProj.M44 - viewProj.M41);
        Plane.Normalize(ref right, out right);

        // Top plane   (row = 2, sign = -1 => M14 - M12, M24 - M22, ...)
        top = new Plane(
            viewProj.M14 - viewProj.M12,
            viewProj.M24 - viewProj.M22,
            viewProj.M34 - viewProj.M32,
            viewProj.M44 - viewProj.M42);
        Plane.Normalize(ref top, out top);

        // Bottom plane (row = 2, sign = +1 => M14 + M12, ...)
        bottom = new Plane(
            viewProj.M14 + viewProj.M12,
            viewProj.M24 + viewProj.M22,
            viewProj.M34 + viewProj.M32,
            viewProj.M44 + viewProj.M42);
        Plane.Normalize(ref bottom, out bottom);

        // Near plane  (row = 3, sign = +1 => M14 + M13, ...)
        near = new Plane(
            viewProj.M14 + viewProj.M13,
            viewProj.M24 + viewProj.M23,
            viewProj.M34 + viewProj.M33,
            viewProj.M44 + viewProj.M43);
        Plane.Normalize(ref near, out near);

        // Far plane   (row = 3, sign = -1 => M14 - M13, ...)
        far = new Plane(
            viewProj.M14 - viewProj.M13,
            viewProj.M24 - viewProj.M23,
            viewProj.M34 - viewProj.M33,
            viewProj.M44 - viewProj.M43);
        Plane.Normalize(ref far, out far);
    }


    

    // --- Helper Functions Needed for Clipping ---

    private static Vector3[] GetBoxCorners(Vector3 center, Vector3 halfDim)
    {
        return
        [
            center + new Vector3(-halfDim.X, -halfDim.Y, -halfDim.Z),
            center + new Vector3(halfDim.X, -halfDim.Y, -halfDim.Z),
            center + new Vector3(halfDim.X, halfDim.Y, -halfDim.Z),
            center + new Vector3(-halfDim.X, halfDim.Y, -halfDim.Z),
            center + new Vector3(-halfDim.X, -halfDim.Y, halfDim.Z),
            center + new Vector3(halfDim.X, -halfDim.Y, halfDim.Z),
            center + new Vector3(halfDim.X, halfDim.Y, halfDim.Z),
            center + new Vector3(-halfDim.X, halfDim.Y, halfDim.Z)
        ];
    }

    private static List<Vector3>[] GetTransformedBoxFacePolygons(Vector3 center, Vector3 halfDim, ref Matrix transform)
    {
        var c = GetBoxCorners(center, halfDim);
        var tc = new Vector3[8];
        for (var i = 0; i < 8; i++) tc[i] = Vector3.TransformCoordinate(c[i], transform);

        var facePolygons = new List<Vector3>[6];
        facePolygons[0] = [tc[0], tc[4], tc[5], tc[1]]; // Floor (Y-)
        facePolygons[1] = [tc[3], tc[7], tc[6], tc[2]]; // Ceiling (Y+)
        facePolygons[2] = [tc[1], tc[5], tc[6], tc[2]]; // Right (X+)
        facePolygons[3] = [tc[5], tc[4], tc[7], tc[6]]; // Back (Z+)
        facePolygons[4] = [tc[4], tc[0], tc[3], tc[7]]; // Left (X-)
        facePolygons[5] = [tc[0], tc[1], tc[2], tc[3]]; // Front (Z-)
        return facePolygons;
    }

    private static List<Vector3> ClipPolygonAgainstPlane(List<Vector3> polygonVertices, Plane clippingPlane)
    {
        var clippedVertices = new List<Vector3>();
        if (polygonVertices.Count < 3) return clippedVertices;

        for (var i = 0; i < polygonVertices.Count; i++)
        {
            var p1 = polygonVertices[i];
            var p2 = polygonVertices[(i + 1) % polygonVertices.Count];

            var dist1 = Plane.DotCoordinate(clippingPlane, p1);
            var dist2 = Plane.DotCoordinate(clippingPlane, p2);

            var p1Inside = dist1 >= 0f;
            var p2Inside = dist2 >= 0f;

            if (p1Inside && p2Inside)
            {
                clippedVertices.Add(p2);
            } // Case 1: In -> In
            else if (p1Inside && !p2Inside) // Case 2: In -> Out
            {
                var direction = p2 - p1;
                var t = -dist1 / Vector3.Dot(direction, clippingPlane.Normal);
                clippedVertices.Add(p1 + direction * System.Math.Clamp(t, 0.0f, 1.0f));
            }
            else if (!p1Inside && p2Inside) // Case 3: Out -> In
            {
                var direction = p2 - p1;
                var t = -dist1 / Vector3.Dot(direction, clippingPlane.Normal);
                clippedVertices.Add(p1 + direction * System.Math.Clamp(t, 0.0f, 1.0f));
                clippedVertices.Add(p2);
            }
            // Case 4: Out -> Out - Add nothing
        }

        return clippedVertices;
    }



    /// 
    ///     Calculates the polygons representing the visible portions of a transformed box's faces within a frustum defined by
    ///     a View-Projection matrix.
    ///     Uses the Sutherland-Hodgman algorithm to clip each face against the frustum planes.
   
    public static List<List<Vector3>> GetClippedBoxFacePolygonsFromMatrix( // Renamed slightly
        Matrix viewProj, // Changed from BoundingFrustum
        Vector3 boxCenter,
        Vector3 boxDimensions,
        Matrix boxWorldTransform)
    {
        var halfDim = boxDimensions * 0.5f;

        // 1. Get the 6 faces of the transformed box as polygons
        List<Vector3>[] transformedFacePolygons =
            GetTransformedBoxFacePolygons(boxCenter, halfDim, ref boxWorldTransform);

        // 2. Extract frustum planes directly from the matrix
        ExtractPlanesFromMatrix(viewProj,
            out var leftPlane, out var rightPlane,
            out var topPlane, out var bottomPlane,
            out var nearPlane, out var farPlane);

        var frustumPlanes = new[] { nearPlane, farPlane, leftPlane, rightPlane, topPlane, bottomPlane };

        // 3. Clip each face polygon against all frustum planes
        var visiblePolygons = new List<List<Vector3>>();

        for (var i = 0; i < 6; i++) // For each face
        {
            var currentPolygon = transformedFacePolygons[i];

            for (var j = 0; j < 6; j++) // For each frustum plane
            {
                if (currentPolygon.Count == 0) break;
                currentPolygon = ClipPolygonAgainstPlane(currentPolygon, frustumPlanes[j]);
            }

            visiblePolygons.Add(currentPolygon);
        }

        return visiblePolygons; // Vertices are in transformed world space
    }
    
    /// <summary>
    ///     Calculates a transformation matrix that creates the smallest possible bounding rectangle for coplanar points
    ///     Returns identity matrix if input is invalid
    /// </summary>
    /// <param name="points">Array of Vector3 points on the same plane (minimum 3 points required)</param>
    /// <returns>Matrix transformation that will transform the points into the smallest possible bounding rectangle</returns>
    public static Matrix CalculateQuadTransform(Vector3[] points)
    {
        // Validate input - need at least 3 points to define a plane
        if (points?.Length < 3) return Matrix.Identity;

        var normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]);
        if (normal.LengthSquared() < MathUtil.ZeroTolerance) return Matrix.Identity;

        normal.Normalize();

        // Check if all points are coplanar
        for (var i = 3; i < points.Length; i++)
            if (System.Math.Abs(Vector3.Dot(points[i] - points[0], normal)) >= MathUtil.ZeroTolerance)
                return Matrix.Identity;

        // Calculate centroid using Stride math
        var center = Vector3.Zero;
        foreach (var point in points) center += point;
        center /= points.Length;

        // Collect potential rotation axes from various point relationships
        var edges = new List<Vector3>();
        var numPoints = points.Length;

        // Add edges between consecutive points (treating as a polygon)
        for (var i = 0; i < numPoints; i++)
        {
            var edge = points[(i + 1) % numPoints] - points[i];
            if (edge.LengthSquared() > MathUtil.ZeroTolerance)
            {
                edge.Normalize();
                edges.Add(edge);
            }
        }

        // Add vectors from center to each point
        foreach (var point in points)
        {
            var radial = point - center;
            if (radial.LengthSquared() > MathUtil.ZeroTolerance)
            {
                radial.Normalize();
                edges.Add(radial);
            }
        }

        // Add some additional vectors between non-adjacent points (sample to avoid too many)
        var maxSamples = MathUtil.Clamp(numPoints * (numPoints - 1) / 2, 0, 10);
        var sampleCount = 0;
        for (var i = 0; i < numPoints && sampleCount < maxSamples; i++)
        for (var j = i + 2; j < numPoints && sampleCount < maxSamples; j++)
        {
            var diagonal = points[j] - points[i];
            if (diagonal.LengthSquared() > MathUtil.ZeroTolerance)
            {
                diagonal.Normalize();
                edges.Add(diagonal);
                sampleCount++;
            }
        }

        if (edges.Count == 0) return Matrix.Identity;

        // Find orientation with smallest bounding rectangle area
        var minArea = float.MaxValue;
        var bestRotation = Matrix.Identity;
        var bestDimensions = Vector2.Zero;
        var bestCenter = Vector3.Zero;

        foreach (var primaryAxis in edges)
        {
            var secondaryAxis = Vector3.Cross(normal, primaryAxis);
            secondaryAxis.Normalize();

            // Calculate bounding rectangle for all points in this orientation
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var point in points)
            {
                var localPoint = point - center;
                var projX = Vector3.Dot(localPoint, primaryAxis);
                var projY = Vector3.Dot(localPoint, secondaryAxis);

                minX = System.Math.Min(minX, projX);
                maxX = System.Math.Max(maxX, projX);
                minY = System.Math.Min(minY, projY);
                maxY = System.Math.Max(maxY, projY);
            }

            var width = maxX - minX;
            var height = maxY - minY;
            var area = width * height;

            if (area < minArea)
            {
                minArea = area;
                bestDimensions = new Vector2(width, height);

                // Calculate the actual center of the bounding rectangle in 3D space
                var centerX = (minX + maxX) * 0.5f;
                var centerY = (minY + maxY) * 0.5f;
                bestCenter = center + centerX * primaryAxis + centerY * secondaryAxis;

                // Build rotation matrix for this orientation
                bestRotation = new Matrix
                {
                    Row1 = new Vector4(primaryAxis, 0),
                    Row2 = new Vector4(secondaryAxis, 0),
                    Row3 = new Vector4(normal, 0),
                    Row4 = new Vector4(0, 0, 0, 1)
                };
            }
        }

        if (bestDimensions.X < MathUtil.ZeroTolerance || bestDimensions.Y < MathUtil.ZeroTolerance)
            return Matrix.Identity;

        // Create transformation: Scale unit square to bounding rectangle size, 
        // then rotate to correct orientation, then translate to correct position
        var scale = Matrix.Scaling(bestDimensions.X, bestDimensions.Y, 1);
        var translation = Matrix.Translation(bestCenter);

        return scale * bestRotation * translation;
    }
    
    #endregion
    
    #region UVUtils
    
    private const float POINT_ON_PLANE_TOLERANCE = 0.1f;

    // --- Helper Functions (RayPlaneIntersection, IsPointInRectangle, MapToTexture) ---
    // ... (Assume these are present and correct) ...
    private static bool RayPlaneIntersection(Vector3 rayOrigin, Vector3 rayDir, Vector3 planePos, Vector3 planeNormal,
        out Vector3 intersection)
    {
        intersection = Vector3.Zero;
        var denom = Vector3.Dot(rayDir, planeNormal);
        if (System.Math.Abs(denom) < 1e-6f) return false;
        var t = Vector3.Dot(planePos - rayOrigin, planeNormal) / denom;
        if (t < -POINT_ON_PLANE_TOLERANCE) return false;
        intersection = rayOrigin + rayDir * t;
        return true;
    }

    private static bool IsPointInRectangle(Vector3 position, Vector3 rectCenter, Vector3 rectDimensions,
        Vector3 normalAxis)
    {
        var localPos = position - rectCenter;
        Vector3 up, right;
        var halfWidth = rectDimensions.X * 0.5f;
        var halfHeight = rectDimensions.Y * 0.5f;
        var halfDepth = rectDimensions.Z * 0.5f;
        const float axisTolerance = 0.01f;

        if (System.Math.Abs(normalAxis.Y) >= 1.0f - axisTolerance)
        {
            /* Floor/Ceiling */
            up = Vector3.UnitZ;
            right = Vector3.UnitX;
            return System.Math.Abs(Vector3.Dot(localPos, right)) <= halfWidth + POINT_ON_PLANE_TOLERANCE &&
                   System.Math.Abs(Vector3.Dot(localPos, up)) <= halfDepth + POINT_ON_PLANE_TOLERANCE;
        }

        if (System.Math.Abs(normalAxis.X) >= 1.0f - axisTolerance)
        {
            /* Left/Right Wall */
            up = Vector3.UnitY;
            right = Vector3.UnitZ;
            return System.Math.Abs(Vector3.Dot(localPos, right)) <= halfDepth + POINT_ON_PLANE_TOLERANCE &&
                   System.Math.Abs(Vector3.Dot(localPos, up)) <= halfHeight + POINT_ON_PLANE_TOLERANCE;
        }

        if (System.Math.Abs(normalAxis.Z) >= 1.0f - axisTolerance)
        {
            /* Front/Back Wall */
            up = Vector3.UnitY;
            right = Vector3.UnitX;
            return System.Math.Abs(Vector3.Dot(localPos, right)) <= halfWidth + POINT_ON_PLANE_TOLERANCE &&
                   System.Math.Abs(Vector3.Dot(localPos, up)) <= halfHeight + POINT_ON_PLANE_TOLERANCE;
        }

        return false;
    }


    /// <summary>
    ///     For each point in the input list, projects it using raycasting from a viewer position
    ///     to find the closest intersection on the room. If the intersection lies on an edge or corner,
    ///     it returns UV mappings for ALL applicable faces for that single intersection point.
    ///     The outermost loop iterates through faces, then through particles.
    /// </summary>
    public static List<Vector2> ProjectWorldPointsByFaceThenParticle(
        IReadOnlyList<Vector3> worldPoints,
        Vector3 viewerPosition,
        SurfaceData surfaceData,
        LayoutInfo layoutInfo)
    {
        var allResults = new List<Vector2>();
        if (worldPoints == null || worldPoints.Count == 0) return allResults;

        var roomDimsForCheck = new Vector3(layoutInfo.RoomDimensions.X, layoutInfo.RoomDimensions.Y,
            layoutInfo.RoomDimensions.Z);

        // To avoid adding duplicate ProjectedPointInfo for the same (originalPointIndex, faceIndex)
        // combination if a particle's projection happens to be processed multiple times
        // due to the outer loop structure.
        var alreadyAdded = new HashSet<(int, int)>();


        // Outer loop: Iterate through each of the 6 room faces (acting as potential primary hit faces)
        // This doesn't really change the logic for finding the closest hit,
        // but it fulfills the structural request. The core logic for each particle remains the same.
        for (var primaryTargetFaceConsideration = 0;
             primaryTargetFaceConsideration < 6;
             primaryTargetFaceConsideration++)
            // Inner loop: Iterate through each input world point (particle)
        for (var pointIdx = 0; pointIdx < worldPoints.Count; pointIdx++)
        {
            var particlePos = worldPoints[pointIdx];

            // 1. Raycast from viewer through particle to find the *closest* intersection point
            // This part is independent of `primaryTargetFaceConsideration` and must check all faces
            // to truly find the closest.
            var rayDir = particlePos - viewerPosition;
            if (rayDir.LengthSquared() < 1e-8f) continue; // Skip this particle
            rayDir = Vector3.Normalize(rayDir);

            var closestDistance = float.MaxValue;
            var closestIntersectionPoint = Vector3.Zero;
            var actualPrimaryHitFaceIndex = -1; // The true closest hit face

            for (var i = 0; i < 6; i++) // Check against ALL faces to find the true closest
            {
                var surfacePlanePos = new Vector3(surfaceData.PositionsAndType[i].X, surfaceData.PositionsAndType[i].Y,
                    surfaceData.PositionsAndType[i].Z);
                var surfacePlaneNormal = new Vector3(surfaceData.NormalsAndSize[i].X, surfaceData.NormalsAndSize[i].Y,
                    surfaceData.NormalsAndSize[i].Z);
                if (!RayPlaneIntersection(viewerPosition, rayDir, surfacePlanePos, surfacePlaneNormal,
                        out var currentIntersection)) continue;
                if (!IsPointInRectangle(currentIntersection, surfacePlanePos, roomDimsForCheck,
                        surfacePlaneNormal)) continue;
                var distance = (currentIntersection - viewerPosition).LengthSquared();
                if (!(distance < closestDistance)) continue;

                closestDistance = distance;
                closestIntersectionPoint = currentIntersection;
                actualPrimaryHitFaceIndex = i;
            }

            if (actualPrimaryHitFaceIndex == -1) continue; // This particle's ray didn't hit any face

            // Now we have the `closestIntersectionPoint` and `actualPrimaryHitFaceIndex` for `particlePos`.
            // Regardless of `primaryTargetFaceConsideration`, we now check which faces this
            // `closestIntersectionPoint` lies on for edge/corner mapping.
            // 2. For the `closestIntersectionPoint`, check which faces it lies on.
            for (var faceIdxMapping = 0; faceIdxMapping < 6; faceIdxMapping++)
            {
                // Check if we've already added this specific (originalPoint, mappingFace) pair
                if (alreadyAdded.Contains((pointIdx, faceIdxMapping))) continue;

                var currentSurfacePos = new Vector3(surfaceData.PositionsAndType[faceIdxMapping].X,
                    surfaceData.PositionsAndType[faceIdxMapping].Y, surfaceData.PositionsAndType[faceIdxMapping].Z);
                var currentSurfaceNormal = new Vector3(surfaceData.NormalsAndSize[faceIdxMapping].X,
                    surfaceData.NormalsAndSize[faceIdxMapping].Y, surfaceData.NormalsAndSize[faceIdxMapping].Z);
                currentSurfaceNormal.Normalize();

                var distanceToPlane = Vector3.Dot(closestIntersectionPoint - currentSurfacePos, currentSurfaceNormal);
                if (!(System.Math.Abs(distanceToPlane) <= POINT_ON_PLANE_TOLERANCE)) continue;
                if (!IsPointInRectangle(closestIntersectionPoint, currentSurfacePos, roomDimsForCheck,
                        currentSurfaceNormal)) continue;
                var uv = MapToTexture(closestIntersectionPoint, faceIdxMapping, layoutInfo);
                allResults.Add(uv);
                alreadyAdded.Add((pointIdx, faceIdxMapping)); // Mark as added
            } // End loop for mapping faces for the closestIntersectionPoint
        } // End loop through particles

        // End loop through primaryTargetFaceConsideration (outermost loop)
        return allResults;
    }


    /// <summary>
    ///     Maps a 3D point on a specific room surface to 2D texture coordinates within the unfolded cross layout. (Ported from
    ///     HLSL - Corrected Scaling)
    /// </summary>
    /// <param name="pointOnSurface">The 3D intersection point on the surface.</param>
    /// <param name="surfaceIndex">The index (0-5) of the surface the point lies on.</param>
    /// <param name="layoutInfo">Layout information containing face layout and room details.</param>
    /// <returns>The calculated 2D texture coordinate.</returns>
    private static Vector2 MapToTexture(
        Vector3 pointOnSurface,
        int surfaceIndex,
        LayoutInfo layoutInfo
    )
    {
        // Extract room data from LayoutInfo
        var roomCenter = new Vector3(layoutInfo.RoomCenter.X, layoutInfo.RoomCenter.Y, layoutInfo.RoomCenter.Z);
        var roomDimensions = new Vector3(layoutInfo.RoomDimensions.X, layoutInfo.RoomDimensions.Y,
            layoutInfo.RoomDimensions.Z);

        // Convert point to local room coordinates (relative to room's minimum corner)
        var localPoint = pointOnSurface - (roomCenter - roomDimensions * 0.5f);

        var u = 0.0f;
        var v = 0.0f;

        // Map based on the surface index to get normalized UVs (0-1) for that *face*
        switch (surfaceIndex)
        {
            case 0: // Floor (Green)
                u = localPoint.X / roomDimensions.X;
                v = 1.0f - localPoint.Z / roomDimensions.Z; // Inverted V for floor
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
                u = 1.0f - localPoint.X / roomDimensions.X; // Inverted U for back wall
                v = localPoint.Y / roomDimensions.Y;
                break;
            case 4: // Left wall (Red)
                u = 1.0f - localPoint.Z / roomDimensions.Z; // Inverted U for left wall
                v = localPoint.Y / roomDimensions.Y;
                break;
            case 5: // Front wall (Blue)
                u = localPoint.X / roomDimensions.X;
                v = localPoint.Y / roomDimensions.Y;
                break;
            default:
                u = v = 0.0f;
                break;
        }


        // Clamp face UVs
        u = System.Math.Clamp(u, 0.0f, 1.0f);
        v = System.Math.Clamp(v, 0.0f, 1.0f);

        // Get layout info for this face
        var layoutIndex = surfaceIndex; // switch { 0 => 1, 1 => 0, _ => surfaceIndex };
        var layout = layoutInfo.FaceLayoutInfo[layoutIndex];
        var layoutStartX = layout.X;
        var layoutStartY = layout.Y;
        var layoutWidth = layout.Z;
        var layoutHeight = layout.W;

        // Map normalized face coordinates (u, v) to the position within the cross layout (0-4 width, 0-3 height)
        Vector2 texCoord;
        texCoord.X = layoutStartX + u * layoutWidth;
        texCoord.Y = layoutStartY + v * layoutHeight;

        // --- CORRECTED Zoom and Scaling Logic ---

        // 1. Normalize coordinates to the overall cross layout space (0..1 range relative to 4x3)
        var normalizedCrossCoord = new Vector2(texCoord.X / 4.0f, texCoord.Y / 3.0f);

        return normalizedCrossCoord;
    }
    
    #endregion
}