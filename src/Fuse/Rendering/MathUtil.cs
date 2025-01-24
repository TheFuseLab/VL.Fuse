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
    
    public static Matrix CreateHybridProjectionMatrix(float horizontalFovDegrees, float orthographicHeight, float nearPlane, float farPlane)
    {
        // Ensure valid input parameters
        if (nearPlane <= 0 || farPlane <= nearPlane)
            throw new ArgumentException("Invalid near/far plane values");
    
        if (horizontalFovDegrees <= 0 || horizontalFovDegrees >= 180)
            throw new ArgumentException("FOV must be between 0 and 180 degrees");

        // Perspective calculations for width
        float tanHalfFov = MathF.Tan(horizontalFovDegrees * 0.5f * MathF.PI / 180f);
        float right = tanHalfFov * nearPlane;
        float left = -right;

        // Orthographic calculations for height
        float top = orthographicHeight * 0.5f;
        float bottom = -top;

        // Calculate matrix components
        float x = (2.0f * nearPlane) / (right - left);
        float y = 2.0f / (top - bottom);
        float a = (right + left) / (right - left);
        float b = (top + bottom) / (top - bottom);
        float c = -(farPlane + nearPlane) / (farPlane - nearPlane);
        float d = -(2.0f * farPlane * nearPlane) / (farPlane - nearPlane);

        return new Matrix(
            x,    0.0f,  0.0f,  0.0f,
            0.0f,  y,    0.0f,  0.0f,
            a,     b,     c,    -1.0f,
            0.0f,  0.0f,  d,     0.0f
        );
    }
}