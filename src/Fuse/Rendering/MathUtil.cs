using System;
using Stride.Core.Mathematics;

namespace Fuse.Rendering;

public static class MathUtil
{
    public static Matrix CreateCustomProjectionMatrix(float horizontalFovDegrees, float orthographicHeight,
        float nearPlane, float farPlane)
    {
        // Perspective in width
        var right = MathF.Tan(horizontalFovDegrees / 2 * MathF.PI / 180f) * nearPlane;
        var left = -right;

        // Orthographic in height
        var top = orthographicHeight / 2.0f;
        var bottom = -top;

        var xScale = 2.0f * nearPlane / (right - left);
        var yScale = 2.0f / (top - bottom);
        var zScale = farPlane / (farPlane - nearPlane);
        var zOffset = -nearPlane * zScale;

        return new Matrix(
            xScale, 0.0f, 0.0f, 0.0f,
            0.0f, yScale, 0.0f, 0.0f,
            (right + left) / (right - left), (top + bottom) / (top - bottom), zScale, 1.0f,
            0.0f, 0.0f, zOffset, 0.0f
        );
    }

    public static Matrix CreateHybridProjectionMatrix(float horizontalFovDegrees, float orthographicHeight,
        float nearPlane, float farPlane)
    {
        // Ensure valid input parameters
        if (nearPlane <= 0 || farPlane <= nearPlane)
            throw new ArgumentException("Invalid near/far plane values");

        if (horizontalFovDegrees <= 0 || horizontalFovDegrees >= 180)
            throw new ArgumentException("FOV must be between 0 and 180 degrees");

        // Perspective calculations for width
        var tanHalfFov = MathF.Tan(horizontalFovDegrees * 0.5f * MathF.PI / 180f);
        var right = tanHalfFov * nearPlane;
        var left = -right;

        // Orthographic calculations for height
        var top = orthographicHeight * 0.5f;
        var bottom = -top;

        // Calculate matrix components
        var x = 2.0f * nearPlane / (right - left);
        var y = 2.0f / (top - bottom);
        var a = (right + left) / (right - left);
        var b = (top + bottom) / (top - bottom);
        var c = -(farPlane + nearPlane) / (farPlane - nearPlane);
        var d = -(2.0f * farPlane * nearPlane) / (farPlane - nearPlane);

        return new Matrix(
            x, 0.0f, 0.0f, 0.0f,
            0.0f, y, 0.0f, 0.0f,
            a, b, c, -1.0f,
            0.0f, 0.0f, d, 0.0f
        );
    }
}