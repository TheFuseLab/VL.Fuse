using System;
using Stride.Core.Mathematics;

namespace Fuse.Rendering;

public static class MathUtil
{
    public static Matrix CreateCustomProjectionMatrix(float horizontalFovDegrees, float orthographicHeight, float nearPlane, float farPlane)
    {
        

        // Perspective in width
        float right = MathF.Tan(horizontalFovDegrees / 2 * MathF.PI / 180f) * nearPlane;
        float left = -right;

        // Orthographic in height
        float top = orthographicHeight / 2.0f;
        float bottom = -top;

        float xScale = (2.0f * nearPlane) / (right - left);
        float yScale = 2.0f / (top - bottom);
        float zScale = farPlane / (farPlane - nearPlane);
        float zOffset = -nearPlane * zScale;

        return new Matrix(
            xScale, 0.0f, 0.0f, 0.0f,
            0.0f, yScale, 0.0f, 0.0f,
            (right + left) / (right - left), (top + bottom) / (top - bottom), zScale, 1.0f,
            0.0f, 0.0f, zOffset, 0.0f
        );
    }
}