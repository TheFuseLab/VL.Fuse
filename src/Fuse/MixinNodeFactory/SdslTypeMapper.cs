using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Fuse.compute;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse.MixinNodeFactory;

/// <summary>
/// Maps SDSL type strings to C# types and provides default values.
/// Reverse mapping from TypeRegistry (which maps C# types to GPU types).
/// </summary>
public static class SdslTypeMapper
{
    /// <summary>
    /// Maps SDSL type names to C# types.
    /// </summary>
    private static readonly Dictionary<string, Type> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Float types
        ["float"] = typeof(float),
        ["float2"] = typeof(Vector2),
        ["float3"] = typeof(Vector3),
        ["float4"] = typeof(Vector4),

        // Integer types
        ["int"] = typeof(int),
        ["int2"] = typeof(Int2),
        ["int3"] = typeof(Int3),
        ["int4"] = typeof(Int4),
        ["uint"] = typeof(uint),
        ["ushort"] = typeof(ushort),

        // Boolean
        ["bool"] = typeof(bool),

        // Matrix types
        ["float2x2"] = typeof(Matrix2),
        ["float3x3"] = typeof(Matrix3),
        ["float4x4"] = typeof(Matrix),

        // Texture types - all map to Texture
        ["Texture2D"] = typeof(Texture),
        ["Texture3D"] = typeof(Texture),
        ["TextureCube"] = typeof(Texture),
        ["Texture1D"] = typeof(Texture),
        ["Texture2DArray"] = typeof(Texture),
        ["RWTexture2D"] = typeof(Texture),
        ["RWTexture3D"] = typeof(Texture),

        // Sampler
        ["SamplerState"] = typeof(SamplerState),
        ["sampler"] = typeof(SamplerState),

        // Void
        ["void"] = typeof(GpuVoid),
    };

    /// <summary>
    /// Default values for each type.
    /// </summary>
    private static readonly Dictionary<Type, object> DefaultValues = new()
    {
        [typeof(float)] = 0f,
        [typeof(Vector2)] = Vector2.Zero,
        [typeof(Vector3)] = Vector3.Zero,
        [typeof(Vector4)] = Vector4.Zero,
        [typeof(int)] = 0,
        [typeof(Int2)] = Int2.Zero,
        [typeof(Int3)] = Int3.Zero,
        [typeof(Int4)] = Int4.Zero,
        [typeof(uint)] = 0u,
        [typeof(ushort)] = (ushort)0,
        [typeof(bool)] = false,
        [typeof(Matrix2)] = default(Matrix2),
        [typeof(Matrix3)] = default(Matrix3),
        [typeof(Matrix)] = Matrix.Identity,
        [typeof(Texture)] = null!,
        [typeof(SamplerState)] = null!,
        [typeof(GpuVoid)] = null!,
    };

    /// <summary>
    /// Gets the C# type for an SDSL type string.
    /// </summary>
    /// <param name="sdslType">The SDSL type (e.g., "float3", "Texture2D").</param>
    /// <returns>The corresponding C# type, or null if not found.</returns>
    public static Type? GetClrType(string sdslType)
    {
        if (string.IsNullOrWhiteSpace(sdslType))
            return null;

        // Clean up the type string
        var cleanType = sdslType.Trim();

        // Handle generic texture types like Texture2D<float4>
        var genericMatch = Regex.Match(cleanType, @"^(\w+)<.*>$");
        if (genericMatch.Success)
        {
            cleanType = genericMatch.Groups[1].Value;
        }

        // Handle buffer types
        if (cleanType.StartsWith("StructuredBuffer", StringComparison.OrdinalIgnoreCase) ||
            cleanType.StartsWith("RWStructuredBuffer", StringComparison.OrdinalIgnoreCase) ||
            cleanType.StartsWith("Buffer", StringComparison.OrdinalIgnoreCase) ||
            cleanType.StartsWith("RWBuffer", StringComparison.OrdinalIgnoreCase) ||
            cleanType.StartsWith("AppendStructuredBuffer", StringComparison.OrdinalIgnoreCase) ||
            cleanType.StartsWith("ConsumeStructuredBuffer", StringComparison.OrdinalIgnoreCase))
        {
            return typeof(Buffer);
        }

        return TypeMap.TryGetValue(cleanType, out var type) ? type : null;
    }

    /// <summary>
    /// Gets the element type of a buffer type (e.g., float from StructuredBuffer&lt;float&gt;).
    /// </summary>
    public static Type? GetBufferElementType(string sdslType)
    {
        if (string.IsNullOrWhiteSpace(sdslType))
            return null;

        // Extract the generic type parameter: StructuredBuffer<float> -> float
        var match = Regex.Match(sdslType.Trim(), @"<\s*(\w+)\s*>$");
        if (!match.Success)
            return null;

        var elementTypeName = match.Groups[1].Value;
        return GetClrType(elementTypeName);
    }

    /// <summary>
    /// Gets the default value for an SDSL type.
    /// </summary>
    /// <param name="sdslType">The SDSL type string.</param>
    /// <returns>The default value, or null if not found.</returns>
    public static object? GetDefaultValue(string sdslType)
    {
        var clrType = GetClrType(sdslType);
        if (clrType == null)
            return null;

        return DefaultValues.TryGetValue(clrType, out var value) ? value : null;
    }

    /// <summary>
    /// Gets the default value for a C# type.
    /// </summary>
    /// <param name="clrType">The C# type.</param>
    /// <returns>The default value, or null if not found.</returns>
    public static object? GetDefaultValue(Type clrType)
    {
        return DefaultValues.TryGetValue(clrType, out var value) ? value : null;
    }

    /// <summary>
    /// Parses an SDSL default value string into a C# value.
    /// </summary>
    /// <param name="sdslType">The SDSL type.</param>
    /// <param name="valueString">The value string (e.g., "1.0", "float3(1,0,0)").</param>
    /// <returns>The parsed value, or null if parsing fails.</returns>
    public static object? ParseValue(string sdslType, string valueString)
    {
        if (string.IsNullOrWhiteSpace(valueString))
            return GetDefaultValue(sdslType);

        var clrType = GetClrType(sdslType);
        if (clrType == null)
            return null;

        valueString = valueString.Trim();

        try
        {
            // Handle scalar types
            if (clrType == typeof(float))
            {
                // Remove 'f' suffix if present
                var cleanValue = valueString.TrimEnd('f', 'F');
                return float.Parse(cleanValue, CultureInfo.InvariantCulture);
            }

            if (clrType == typeof(int))
            {
                return int.Parse(valueString, CultureInfo.InvariantCulture);
            }

            if (clrType == typeof(uint))
            {
                var cleanValue = valueString.TrimEnd('u', 'U');
                return uint.Parse(cleanValue, CultureInfo.InvariantCulture);
            }

            if (clrType == typeof(bool))
            {
                return valueString.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            // Handle vector types with constructor syntax: float3(x, y, z)
            var vectorMatch = Regex.Match(valueString, @"^\w+\s*\(\s*(.+)\s*\)$");
            if (vectorMatch.Success)
            {
                var components = vectorMatch.Groups[1].Value.Split(',');
                var floatComponents = new float[components.Length];
                for (var i = 0; i < components.Length; i++)
                {
                    var comp = components[i].Trim().TrimEnd('f', 'F');
                    floatComponents[i] = float.Parse(comp, CultureInfo.InvariantCulture);
                }

                if (clrType == typeof(Vector2) && floatComponents.Length >= 2)
                    return new Vector2(floatComponents[0], floatComponents[1]);

                if (clrType == typeof(Vector3) && floatComponents.Length >= 3)
                    return new Vector3(floatComponents[0], floatComponents[1], floatComponents[2]);

                if (clrType == typeof(Vector4) && floatComponents.Length >= 4)
                    return new Vector4(floatComponents[0], floatComponents[1], floatComponents[2], floatComponents[3]);

                if (clrType == typeof(Int2) && floatComponents.Length >= 2)
                    return new Int2((int)floatComponents[0], (int)floatComponents[1]);

                if (clrType == typeof(Int3) && floatComponents.Length >= 3)
                    return new Int3((int)floatComponents[0], (int)floatComponents[1], (int)floatComponents[2]);

                if (clrType == typeof(Int4) && floatComponents.Length >= 4)
                    return new Int4((int)floatComponents[0], (int)floatComponents[1], (int)floatComponents[2], (int)floatComponents[3]);
            }

            // Handle single value for vectors (broadcast)
            if (float.TryParse(valueString.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out var singleValue))
            {
                if (clrType == typeof(Vector2))
                    return new Vector2(singleValue);
                if (clrType == typeof(Vector3))
                    return new Vector3(singleValue);
                if (clrType == typeof(Vector4))
                    return new Vector4(singleValue);
            }
        }
        catch
        {
            // Parsing failed, return default
        }

        return GetDefaultValue(clrType);
    }

    /// <summary>
    /// Checks if the given SDSL type is a known type.
    /// </summary>
    public static bool IsKnownType(string sdslType)
    {
        return GetClrType(sdslType) != null;
    }

    /// <summary>
    /// Checks if the given SDSL type is a texture type.
    /// </summary>
    public static bool IsTextureType(string sdslType)
    {
        if (string.IsNullOrWhiteSpace(sdslType))
            return false;

        var cleanType = sdslType.Trim();
        return cleanType.StartsWith("Texture", StringComparison.OrdinalIgnoreCase) ||
               cleanType.StartsWith("RWTexture", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the given SDSL type is a buffer type.
    /// </summary>
    public static bool IsBufferType(string sdslType)
    {
        if (string.IsNullOrWhiteSpace(sdslType))
            return false;

        var cleanType = sdslType.Trim();
        return cleanType.Contains("Buffer", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the given SDSL type is a sampler type.
    /// </summary>
    public static bool IsSamplerType(string sdslType)
    {
        if (string.IsNullOrWhiteSpace(sdslType))
            return false;

        var cleanType = sdslType.Trim().ToLowerInvariant();
        return cleanType == "samplerstate" || cleanType == "sampler";
    }

    /// <summary>
    /// Checks if the given SDSL type is a read-write type (RWBuffer, RWTexture, etc.).
    /// </summary>
    public static bool IsReadWriteType(string sdslType)
    {
        if (string.IsNullOrWhiteSpace(sdslType))
            return false;

        var cleanType = sdslType.Trim();
        return cleanType.StartsWith("RW", StringComparison.OrdinalIgnoreCase) ||
               cleanType.StartsWith("AppendStructuredBuffer", StringComparison.OrdinalIgnoreCase) ||
               cleanType.StartsWith("ConsumeStructuredBuffer", StringComparison.OrdinalIgnoreCase);
    }
}
