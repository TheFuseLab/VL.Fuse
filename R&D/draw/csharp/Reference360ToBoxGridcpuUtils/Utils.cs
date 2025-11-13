// For examples, see:
// https://thegraybook.vvvv.org/reference/extending/writing-nodes.html#examples

namespace Main;

public static class Utils
{
    
    // Function to map a point on a room surface to texture coordinates
    public static Vector4 mapToTexture(
        Vector3 pointOnSurface, 
        int surfaceIndex,
        // Buffer data passed as parameters
        Vector4[] faceLayoutInfo,
        Vector3 roomCenter,
        Vector3 roomDimensions,
        // Zoom parameter (optional)
        Vector4 zoomRegion   // Region to zoom into: (minU, minV, maxU, maxV) in normalized cross layout space
    ) {
        // Convert point to local room coordinates (relative to room's min corner)
        var localPoint = pointOnSurface - (roomCenter - roomDimensions * 0.5f);
        
        var u = 0.0f;
        var v = 0.0f;
        
        // Map based on the surface index
        switch(surfaceIndex) {
            case 0: // Floor (Green)
                u = (localPoint.X / roomDimensions.X);
                v = 1.0f - (localPoint.Z / roomDimensions.Z);
                break;
                
            case 1: // Ceiling (Yellow)
                u = (localPoint.X / roomDimensions.X);
                v = (localPoint.Z / roomDimensions.Z);
                break;
                
            case 2: // Right wall (Magenta)
                u = (localPoint.Z / roomDimensions.Z);
                v = (localPoint.Y / roomDimensions.Y);
                break;
                
            case 3: // Back wall (Cyan)
                u = 1.0f - (localPoint.X / roomDimensions.X);
                v = (localPoint.Y / roomDimensions.Y);
                break;
                
            case 4: // Left wall (Red)
                u = 1.0f - (localPoint.Z / roomDimensions.Z);
                v = (localPoint.Y / roomDimensions.Y);
                break;
                
            case 5: // Front wall (Blue)
                u = (localPoint.X / roomDimensions.X);
                v = (localPoint.Y / roomDimensions.Y);
                break;
                
            default:
                // Default case (shouldn't happen)
                u = v = 0.0f;
                break;
        }

        // Store normalized per-face UVs in [0,1]
        Vector2 faceUV01 = new Vector2(u, v);
        
        // Get layout info for this face (startX, startY, width, height)
        Vector4 layout = faceLayoutInfo[surfaceIndex];
        
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
        return new Vector4(texCoord.X,texCoord.Y, faceUV01.X, faceUV01.Y);
    }
    
    public static Vector3 projectParticleByViewPositionAABB(
        Vector3 particlePos,
        Vector3 roomCenter,
        Vector3 roomDimensions,
        Vector4 zoomRegion,               // same semantics as before
        Vector3 viewerPosition,
        out int   hitSurfaceIndex,
        out Vector2 out_faceUV01,
        Vector4[] faceLayoutInfo
    )
    {
        // safe defaults so HLSL is happy
        hitSurfaceIndex = -1;
        out_faceUV01 = new Vector2(0.0f, 0.0f);

        Vector3 ro = viewerPosition;
        Vector3 rd = Vector3.Normalize(particlePos - viewerPosition);

        Vector3 halfExt = roomDimensions * 0.5f;
        Vector3 boxMin  = roomCenter - halfExt;
        Vector3 boxMax  = roomCenter + halfExt;

        // ---------- slab intersection ----------
        Vector3 invDir = 1.0f / rd;

        Vector3 t0 = (boxMin - ro) * invDir;
        Vector3 t1 = (boxMax - ro) * invDir;

        Vector3 tmin = Vector3.Min(t0, t1);
        Vector3 tmax = Vector3.Max(t0, t1);

        float tEnter = Math.Max(Math.Max(tmin.X, tmin.Y), tmin.Z);
        float tExit  = Math.Min(Math.Min(tmax.X, tmax.Y), tmax.Z);

        // no hit or behind
        if (tExit < 0.0 || tEnter > tExit)
            return new Vector3(0.0f, 0.0f, 0.0f);

        float t = (tEnter > 0.0) ? tEnter : tExit;
        if (t <= 0.0)
            return new Vector3(0.0f, 0.0f, 0.0f);

        Vector3 p = ro + rd * t;

        // ---------- classify face (match your indices) ----------
        const float eps = 1e-4f;

        // 0: floor (Green)    -> y = boxMin.Y
        // 1: ceiling (Yellow) -> y = boxMax.Y
        // 2: right (Magenta)  -> x = boxMax.X
        // 3: back (Cyan)      -> z = boxMax.Z
        // 4: left (Red)       -> x = boxMin.X
        // 5: front (Blue)     -> z = boxMin.Z

        if      (Math.Abs(p.Y - boxMin.Y) < eps) hitSurfaceIndex = 0;
        else if (Math.Abs(p.Y - boxMax.Y) < eps) hitSurfaceIndex = 1;
        else if (Math.Abs(p.X - boxMax.X) < eps) hitSurfaceIndex = 2;
        else if (Math.Abs(p.Z - boxMax.Z) < eps) hitSurfaceIndex = 3;
        else if (Math.Abs(p.X - boxMin.X) < eps) hitSurfaceIndex = 4;
        else if (Math.Abs(p.Z - boxMin.Z) < eps) hitSurfaceIndex = 5;

        if (hitSurfaceIndex < 0)
            return new Vector3(0.0f, 0.0f, 0.0f);

        // ---------- UV & atlas via your existing mapToTexture ----------
        // mapToTexture expects the exact hit point, face index, and your original buffers.
        Vector4 uvPack = mapToTexture(
            p,
            hitSurfaceIndex,
            faceLayoutInfo,
            roomCenter,
            roomDimensions,
            zoomRegion
        );

        out_faceUV01 = new Vector2(uvPack.Z, uvPack.W);

        // IMPORTANT: z matches original: distance from intersection to particle
        float offsetDist = Vector3.Distance(p, particlePos);

        // uvPack.xy is in 4x3 layout space, like before
        return new Vector3(uvPack.X, uvPack.Y, offsetDist);
    }
}