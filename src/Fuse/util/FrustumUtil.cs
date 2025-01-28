using Stride.Core.Extensions;
using Stride.Core.Mathematics;

namespace Fuse.util;

public static class FrustumUtil
{
    /// <summary>
    /// Determines whether a sphere intersects with the frustum defined by the given planes.
    /// </summary>
    /// <param name="planes">An array of six normalized planes defining the frustum.</param>
    /// <param name="sphere">The BoundingSphere to test.</param>
    /// <returns>True if the sphere intersects or is inside the frustum; otherwise, false.</returns>
    public static bool IsSphereInsideFrustum(Plane[] planes, BoundingSphere sphere)
    {
        foreach (var plane in planes)
        {
            // Compute the signed distance from the sphere center to the plane
            var distance = Vector3.Dot(plane.Normal, sphere.Center) + plane.D;

            // If the distance is less than -radius, the sphere is completely outside this plane
            if (distance < -sphere.Radius)
                return false; // Disjoint
        }

        // The sphere is either intersecting or completely inside all planes
        return true;
    }
    
    /// <summary>
    /// Extracts the six normalized planes from the BoundingFrustum.
    /// Order: Left, Right, Top, Bottom, Near, Far
    /// </summary>
    /// <param name="frustum">The BoundingFrustum to extract planes from.</param>
    /// <returns>An array of six normalized planes.</returns>
    public static Plane[] GetFrustumPlanes(BoundingFrustum frustum)
    {

        var planes = new Plane[6];

        // Left Plane
        planes[0] = frustum.LeftPlane;
        planes[0] = Plane.Normalize(planes[0]);

        // Right Plane
        planes[1] = frustum.RightPlane;
        planes[1] = Plane.Normalize(planes[1]);

        // Top Plane
        planes[2] = frustum.TopPlane;
        planes[2] = Plane.Normalize(planes[2]);

        // Bottom Plane
        planes[3] = frustum.BottomPlane;
        planes[3] = Plane.Normalize(planes[3]);

        // Near Plane
        planes[4] = frustum.NearPlane;
        planes[4] = Plane.Normalize(planes[4]);

        // Far Plane
        planes[5] = frustum.FarPlane;
        planes[5] = Plane.Normalize(planes[5]);

        return planes;
    }
}