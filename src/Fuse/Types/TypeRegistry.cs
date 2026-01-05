using System;
using System.Collections.Generic;
using System.Linq;
using Fuse.compute;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Fuse.Types;

/// <summary>
/// Represents a complete type mapping between C# types and GPU types.
/// </summary>
/// <param name="GpuType">The GPU/HLSL type name (e.g., "float3").</param>
/// <param name="DefaultValue">The default value as HLSL string (e.g., "float3(0.0, 0.0, 0.0)").</param>
/// <param name="Signature">The signature name used in shader function names (e.g., "Float3").</param>
/// <param name="CompositionType">The Stride composition type name (e.g., "ComputeFloat3").</param>
/// <param name="Dimension">Number of components (1 for scalar, 3 for float3, etc.).</param>
/// <param name="ByteSize">Size in bytes on the GPU.</param>
/// <param name="PixelFormat">Corresponding pixel format for textures, if applicable.</param>
/// <param name="IsIntegerType">Whether this is an integer type.</param>
public readonly record struct TypeMapping(
    string GpuType,
    string DefaultValue,
    string Signature,
    string CompositionType,
    int Dimension,
    int ByteSize,
    PixelFormat? PixelFormat = null,
    bool IsIntegerType = false
);

/// <summary>
/// Central registry for all type mappings between C# and GPU types.
/// Consolidates type information that was previously scattered across multiple dictionaries.
/// </summary>
/// <remarks>
/// This registry is the single source of truth for type mappings in VL.Fuse.
/// </remarks>
public static class TypeRegistry
{
    private static readonly Dictionary<Type, TypeMapping> Mappings = new()
    {
        // Float types
        [typeof(float)] = new TypeMapping(
            GpuType: "float",
            DefaultValue: "0.0",
            Signature: "Float",
            CompositionType: "ComputeFloat",
            Dimension: 1,
            ByteSize: 4,
            PixelFormat: Stride.Graphics.PixelFormat.R32_Float
        ),
        [typeof(Vector2)] = new TypeMapping(
            GpuType: "float2",
            DefaultValue: "float2(0.0, 0.0)",
            Signature: "Float2",
            CompositionType: "ComputeFloat2",
            Dimension: 2,
            ByteSize: 8,
            PixelFormat: Stride.Graphics.PixelFormat.R32G32_Float
        ),
        [typeof(Vector3)] = new TypeMapping(
            GpuType: "float3",
            DefaultValue: "float3(0.0, 0.0, 0.0)",
            Signature: "Float3",
            CompositionType: "ComputeFloat3",
            Dimension: 3,
            ByteSize: 12,
            PixelFormat: Stride.Graphics.PixelFormat.R32G32B32_Float
        ),
        [typeof(Vector4)] = new TypeMapping(
            GpuType: "float4",
            DefaultValue: "float4(0.0, 0.0, 0.0, 0.0)",
            Signature: "Float4",
            CompositionType: "ComputeFloat4",
            Dimension: 4,
            ByteSize: 16,
            PixelFormat: Stride.Graphics.PixelFormat.R32G32B32A32_Float
        ),
        [typeof(Color4)] = new TypeMapping(
            GpuType: "float4",
            DefaultValue: "float4(0.0, 0.0, 0.0, 0.0)",
            Signature: "Float4",
            CompositionType: "ComputeColor",
            Dimension: 4,
            ByteSize: 16,
            PixelFormat: Stride.Graphics.PixelFormat.R32G32B32A32_Float
        ),

        // Matrix types
        [typeof(Matrix)] = new TypeMapping(
            GpuType: "float4x4",
            DefaultValue: "float4x4()",
            Signature: "Matrix",
            CompositionType: "ComputeMatrix",
            Dimension: 16,
            ByteSize: 64
        ),
        [typeof(Matrix3)] = new TypeMapping(
            GpuType: "float3x3",
            DefaultValue: "float3x3()",
            Signature: "Matrix3",
            CompositionType: "ComputeMatrix",
            Dimension: 9,
            ByteSize: 36
        ),
        [typeof(Matrix2)] = new TypeMapping(
            GpuType: "float2x2",
            DefaultValue: "float2x2()",
            Signature: "Matrix2",
            CompositionType: "ComputeMatrix",
            Dimension: 4,
            ByteSize: 16
        ),

        // Integer types
        [typeof(int)] = new TypeMapping(
            GpuType: "int",
            DefaultValue: "0",
            Signature: "Int",
            CompositionType: "ComputeInt",
            Dimension: 1,
            ByteSize: 4,
            PixelFormat: Stride.Graphics.PixelFormat.R32_SInt,
            IsIntegerType: true
        ),
        [typeof(Int2)] = new TypeMapping(
            GpuType: "int2",
            DefaultValue: "int2(0, 0)",
            Signature: "Int2",
            CompositionType: "ComputeInt2",
            Dimension: 2,
            ByteSize: 8,
            PixelFormat: Stride.Graphics.PixelFormat.R32G32_SInt,
            IsIntegerType: true
        ),
        [typeof(Int3)] = new TypeMapping(
            GpuType: "int3",
            DefaultValue: "int3(0, 0, 0)",
            Signature: "Int3",
            CompositionType: "ComputeInt3",
            Dimension: 3,
            ByteSize: 12,
            PixelFormat: Stride.Graphics.PixelFormat.R32G32B32_SInt,
            IsIntegerType: true
        ),
        [typeof(Int4)] = new TypeMapping(
            GpuType: "int4",
            DefaultValue: "int4(0, 0, 0, 0)",
            Signature: "Int4",
            CompositionType: "ComputeInt4",
            Dimension: 4,
            ByteSize: 16,
            PixelFormat: Stride.Graphics.PixelFormat.R32G32B32A32_SInt,
            IsIntegerType: true
        ),
        [typeof(uint)] = new TypeMapping(
            GpuType: "uint",
            DefaultValue: "0",
            Signature: "UInt",
            CompositionType: "ComputeUint",
            Dimension: 1,
            ByteSize: 4,
            PixelFormat: Stride.Graphics.PixelFormat.R32_UInt,
            IsIntegerType: true
        ),
        [typeof(ushort)] = new TypeMapping(
            GpuType: "ushort",
            DefaultValue: "0",
            Signature: "UShort",
            CompositionType: "ComputeUShort",
            Dimension: 1,
            ByteSize: 2,
            PixelFormat: Stride.Graphics.PixelFormat.R16_UInt,
            IsIntegerType: true
        ),

        // Boolean
        [typeof(bool)] = new TypeMapping(
            GpuType: "bool",
            DefaultValue: "true",
            Signature: "Bool",
            CompositionType: "ComputeBool",
            Dimension: 1,
            ByteSize: 1,
            PixelFormat: Stride.Graphics.PixelFormat.R1_UNorm
        ),

        // Special types
        [typeof(GpuStruct)] = new TypeMapping(
            GpuType: "struct",
            DefaultValue: "",
            Signature: "Struct",
            CompositionType: "struct",
            Dimension: 1,
            ByteSize: 0
        ),
        [typeof(Texture)] = new TypeMapping(
            GpuType: "Texture",
            DefaultValue: "Texture0",
            Signature: "Texture",
            CompositionType: "Texture",
            Dimension: 1,
            ByteSize: 0
        ),
        [typeof(SamplerState)] = new TypeMapping(
            GpuType: "Sampler",
            DefaultValue: "Sampler",
            Signature: "Sampler",
            CompositionType: "Sampler",
            Dimension: 1,
            ByteSize: 0
        ),
        [typeof(GpuVoid)] = new TypeMapping(
            GpuType: "void",
            DefaultValue: "",
            Signature: "Void",
            CompositionType: "ComputeVoid",
            Dimension: 0,
            ByteSize: 0
        )
    };

    // Reverse lookup: PixelFormat -> Type
    private static readonly Dictionary<PixelFormat, Type> PixelFormatToType = new()
    {
        { PixelFormat.R32_Float, typeof(float) },
        { PixelFormat.R32G32_Float, typeof(Vector2) },
        { PixelFormat.R32G32B32_Float, typeof(Vector3) },
        { PixelFormat.R32G32B32A32_Float, typeof(Vector4) },
        { PixelFormat.R32_SInt, typeof(int) },
        { PixelFormat.R32G32_SInt, typeof(Int2) },
        { PixelFormat.R32G32B32_SInt, typeof(Int3) },
        { PixelFormat.R32G32B32A32_SInt, typeof(Int4) },
        { PixelFormat.R32_UInt, typeof(uint) },
        { PixelFormat.R16_UInt, typeof(ushort) },
        { PixelFormat.R1_UNorm, typeof(bool) }
    };

    /// <summary>
    /// Gets the type mapping for a given C# type.
    /// </summary>
    /// <typeparam name="T">The C# type to look up.</typeparam>
    /// <returns>The type mapping.</returns>
    /// <exception cref="NotSupportedException">Thrown when the type is not registered.</exception>
    public static TypeMapping Get<T>() => Get(typeof(T));

    /// <summary>
    /// Gets the type mapping for a given C# type.
    /// </summary>
    /// <param name="type">The C# type to look up.</param>
    /// <returns>The type mapping.</returns>
    /// <exception cref="NotSupportedException">Thrown when the type is not registered.</exception>
    public static TypeMapping Get(Type type)
    {
        if (Mappings.TryGetValue(type, out var mapping))
            return mapping;

        throw new NotSupportedException($"Type {type.FullName} is not registered in TypeRegistry.");
    }

    /// <summary>
    /// Tries to get the type mapping for a given C# type.
    /// </summary>
    /// <param name="type">The C# type to look up.</param>
    /// <param name="mapping">The type mapping if found.</param>
    /// <returns>True if the type was found, false otherwise.</returns>
    public static bool TryGet(Type type, out TypeMapping mapping) => Mappings.TryGetValue(type, out mapping);

    /// <summary>
    /// Tries to get the type mapping for a given C# type.
    /// </summary>
    /// <typeparam name="T">The C# type to look up.</typeparam>
    /// <param name="mapping">The type mapping if found.</param>
    /// <returns>True if the type was found, false otherwise.</returns>
    public static bool TryGet<T>(out TypeMapping mapping) => TryGet(typeof(T), out mapping);

    /// <summary>
    /// Checks if a type is registered in the registry.
    /// </summary>
    public static bool IsRegistered(Type type) => Mappings.ContainsKey(type);

    /// <summary>
    /// Checks if a type is registered in the registry.
    /// </summary>
    public static bool IsRegistered<T>() => IsRegistered(typeof(T));

    /// <summary>
    /// Gets all registered types.
    /// </summary>
    public static IEnumerable<Type> RegisteredTypes => Mappings.Keys;

    #region Convenience Accessors

    /// <summary>Gets the GPU type name for a C# type.</summary>
    public static string GetGpuType(Type type) =>
        TryGet(type, out var m) ? m.GpuType : ShaderNodesUtil.CleanVlClassName(type.Name);

    /// <summary>Gets the GPU type name for a C# type.</summary>
    public static string GetGpuType<T>() => GetGpuType(typeof(T));

    /// <summary>Gets the default value string for a C# type.</summary>
    public static string GetDefaultValue(Type type) =>
        TryGet(type, out var m) ? m.DefaultValue : throw new NotImplementedException($"No default for type: {type.Name}");

    /// <summary>Gets the default value string for a C# type.</summary>
    public static string GetDefaultValue<T>() => GetDefaultValue(typeof(T));

    /// <summary>Gets the signature name for a C# type.</summary>
    public static string GetSignature(Type type) =>
        TryGet(type, out var m) ? m.Signature : throw new NotImplementedException($"No signature for type: {type.Name}");

    /// <summary>Gets the signature name for a C# type.</summary>
    public static string GetSignature<T>() => GetSignature(typeof(T));

    /// <summary>Gets the composition type name for a C# type.</summary>
    public static string GetCompositionType(Type type) =>
        TryGet(type, out var m) ? m.CompositionType : ShaderNodesUtil.CleanVlClassName(type.Name);

    /// <summary>Gets the composition type name for a C# type.</summary>
    public static string GetCompositionType<T>() => GetCompositionType(typeof(T));

    /// <summary>Gets the dimension (component count) for a C# type.</summary>
    public static int GetDimension(Type type) =>
        TryGet(type, out var m) ? m.Dimension : 0;

    /// <summary>Gets the dimension (component count) for a C# type.</summary>
    public static int GetDimension<T>() => GetDimension(typeof(T));

    /// <summary>Gets the byte size for a C# type.</summary>
    public static int GetByteSize(Type type) =>
        TryGet(type, out var m) ? m.ByteSize : 0;

    /// <summary>Gets the byte size for a C# type.</summary>
    public static int GetByteSize<T>() => GetByteSize(typeof(T));

    /// <summary>Gets the pixel format for a C# type.</summary>
    public static PixelFormat GetPixelFormat(Type type) =>
        TryGet(type, out var m) && m.PixelFormat.HasValue ? m.PixelFormat.Value : 0;

    /// <summary>Gets the pixel format for a C# type.</summary>
    public static PixelFormat GetPixelFormat<T>() => GetPixelFormat(typeof(T));

    /// <summary>Checks if a type is an integer type.</summary>
    public static bool IsIntegerType(Type type) =>
        TryGet(type, out var m) && m.IsIntegerType;

    /// <summary>Checks if a type is an integer type.</summary>
    public static bool IsIntegerType<T>() => IsIntegerType(typeof(T));

    /// <summary>Gets the C# type for a pixel format.</summary>
    public static Type GetTypeFromPixelFormat(PixelFormat format) =>
        PixelFormatToType.TryGetValue(format, out var type) ? type : typeof(float);

    #endregion
}
