using System;
using System.Collections.Generic;
using Fuse.compute;
using Fuse.function;
using Fuse.Types;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse;

/// <summary>
/// Utility methods for type conversions between C# types and GPU/shader types.
/// </summary>
/// <remarks>
/// This class delegates to <see cref="TypeRegistry"/> for type mappings.
/// Pattern-matching overloads for <see cref="AbstractShaderNode"/> are provided
/// for runtime type resolution.
/// </remarks>
public static class TypeHelpers
{
    private static readonly Dictionary<Type, string> StructTypes = new();

    #region Type Constraints (USED BY VL)

    // USED BY VL
    // ReSharper disable once UnusedMember.Global
    public static void ConstrainTypes<T>(T input, T input2, T input3, T input4)
    {
    }

    // USED BY VL
    // ReSharper disable once UnusedMember.Global
    public static void ConstrainTypesByEnumerable<T>(T input, IEnumerable<T> input2)
    {
    }

    // USED BY VL
    // ReSharper disable once UnusedMember.Global
    public static void ConstrainTypesByDictionary<T>(T input, IDictionary<string, T> input2)
    {
    }

    // USED BY VL
    // ReSharper disable once UnusedMember.Global
    public static void ConstrainTypeByShaderNode<T>(ShaderNode<T> input, T input2) where T : struct
    {
    }

    // USED BY VL
    // ReSharper disable once UnusedMember.Global
    public static void ConstrainTypesByBuffer<T>(ShaderNode<T> input, ShaderNode<Buffer<T>> input2) where T : unmanaged
    {
    }

    // USED BY VL
    // ReSharper disable once UnusedMember.Global
    public static void ConstrainType<T>(T value, ShaderNode<T> shaderNode, Buffer<T> buffer,
        ShaderNode<Buffer<T>> gpuBuffer) where T : unmanaged
    {
    }

    public static void ConstrainBuffer<T>(T value, ShaderNode<T> shaderNode, Buffer<T> buffer,
        ShaderNode<Buffer<T>> gpuBuffer) where T : unmanaged
    {
    }

    #endregion

    #region Type Checks

    public static bool IsStructType(Type t) => StructTypes.ContainsKey(t);

    public static bool IsStructType<T>() => IsStructType(typeof(T));

    public static bool IsGpuArray(Type type)
    {
        if (type == null) return false;
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(GpuArray<>);
    }

    // TODO: BUG - This method name suggests it checks for GpuArray<T>, but it actually
    // checks IsStructType. This is likely a copy-paste error. Should be:
    // return IsGpuArray(typeof(T));
    // Keeping current behavior to avoid breaking changes - verify before fixing.
    public static bool IsGpuArray<T>() => IsStructType(typeof(T));

    public static bool IsGpuArray<T>(ShaderNode<T> shaderNode) => IsGpuArray(typeof(T));

    public static bool IsGpuArray(AbstractShaderNode theValue)
    {
        if (theValue == null || !theValue.GetType().IsGenericType) return false;
        var genericType = theValue.GetType().GetGenericArguments()[0];
        return genericType.IsGenericType && genericType.GetGenericTypeDefinition() == typeof(GpuArray<>);
    }

    public static bool IsDelegate(AbstractShaderNode theValue)
    {
        return theValue != null && theValue.GetType().IsGenericType &&
               theValue.GetType().GetGenericTypeDefinition() == typeof(Delegate<>);
    }

    public static bool IsVoidNode(AbstractShaderNode theValue) => theValue is ShaderNode<GpuVoid>;

    public static bool IsVoid(Type t) => t == typeof(GpuVoid);

    public static bool IsGpuType(Type type) => TypeRegistry.IsRegistered(type);

    #endregion

    #region Utility Methods

    public static List<string> GetDescription(AbstractShaderNode theValue)
    {
        var keys = new List<string> { "x", "y", "z", "w" };
        var dimension = GetDimension(theValue);
        var result = new List<string>();
        if (dimension == 1)
            result.Add(theValue.Name);
        else
            for (var i = 0; i < dimension; i++)
                result.Add(theValue.Name + "." + keys[i]);
        return result;
    }

    public static ShaderNode<TOut> AdaptiveSignature<TIn, TOut>(string thePrepend, ShaderNode<TIn> theValue,
        out string theSignature)
    {
        theSignature = thePrepend + GetDimension(typeof(TOut)) + GetDimension(typeof(TIn));
        return null;
    }

    public static void LimitToStruct<T>(ShaderNode<T> theValue) where T : struct
    {
    }

    public static string GetGenericTypeName(AbstractShaderNode node)
    {
        if (node == null) return "";
        var objectType = node.GetType();
        var genericArguments = objectType.GetGenericArguments();

        if (genericArguments.Length <= 0) return null;
        var genericType = genericArguments[0];

        if (genericType == typeof(float)) return "Float32";
        if (genericType == typeof(double)) return "Float64";
        if (genericType == typeof(int)) return "Integer32";
        if (genericType == typeof(long)) return "Integer64";
        if (genericType == typeof(Color4)) return "RGBA";

        return genericType.Name;
    }

    public static string ValueToString(object theObject)
    {
        if (theObject is not (byte or sbyte or short or ushort or int or uint or long or ulong))
            return theObject.ToString();

        var number = Convert.ToInt64(theObject);
        return number.ToString("#,##0").Replace(",", ".");
    }

    #endregion

    #region Texture/Buffer Type Names

    public static string TextureTypeName(Texture theTexture, bool theUseRW)
    {
        if (theTexture == null) return "Texture2D<float4>";

        var textureType = theTexture.Dimension switch
        {
            TextureDimension.Texture1D => "Texture1D",
            TextureDimension.Texture2D => theTexture.ArraySize == 1 ? "Texture2D" : "Texture2DArray",
            TextureDimension.Texture3D => "Texture3D",
            TextureDimension.TextureCube => "TextureCube",
            _ => "Texture2D"
        };

        var textureDataType = theTexture.Format switch
        {
            PixelFormat.R32_UInt => "uint",
            PixelFormat.R32_SInt => "int",
            PixelFormat.R32G32_UInt => "uint2",
            PixelFormat.R32G32_SInt => "int2",
            PixelFormat.R32G32B32A32_UInt => "uint4",
            PixelFormat.R32G32B32A32_SInt => "int4",
            PixelFormat.R16_Float => "half",
            PixelFormat.R16G16_Float => "half2",
            PixelFormat.R16G16B16A16_Float => "half4",
            PixelFormat.R32_Float => "float",
            PixelFormat.R32G32_Float => "float2",
            PixelFormat.R32G32B32A32_Float => "float4",
            _ => "float4"
        };

        if ((theTexture.Flags & TextureFlags.UnorderedAccess) == TextureFlags.UnorderedAccess && theUseRW)
            return "RW" + textureType + "<" + textureDataType + ">";

        return textureType + "<" + textureDataType + ">";
    }

    public static string BufferTypeName(Buffer theBuffer, string theType, BufferType theBufferType)
    {
        switch (theBufferType)
        {
            case BufferType.Append:
                return "AppendStructuredBuffer<" + theType + ">";
            case BufferType.Consume:
                return "ConsumeStructuredBuffer<" + theType + ">";
            case BufferType.Normal:
                return "StructuredBuffer<" + theType + ">";
            case BufferType.RW:
                return "RWStructuredBuffer<" + theType + ">";
        }

        // Auto: detect from buffer flags
        if (theBuffer != null && (theBuffer.Flags & BufferFlags.UnorderedAccess) == BufferFlags.UnorderedAccess)
            return "RWStructuredBuffer<" + theType + ">";

        return "StructuredBuffer<" + theType + ">";
    }

    #endregion

    #region GetTypeDefault

    /// <summary>
    /// Gets the default value string for a specific value of type T.
    /// </summary>
    public static string GetDefaultForType<T>(T theValue)
    {
        if (typeof(T) == typeof(float)) return theValue.ToString();

        if (typeof(T) == typeof(Vector2))
        {
            var vec2 = (Vector2)Convert.ChangeType(theValue, typeof(Vector2));
            return $"float2({vec2.X},{vec2.Y})";
        }

        if (typeof(T) == typeof(Vector3))
        {
            var vec3 = (Vector3)Convert.ChangeType(theValue, typeof(Vector3));
            return $"float3({vec3.X},{vec3.Y},{vec3.Z})";
        }

        if (typeof(T) == typeof(Vector4))
        {
            var vec4 = (Vector4)Convert.ChangeType(theValue, typeof(Vector4));
            return $"float4({vec4.X},{vec4.Y},{vec4.Z},{vec4.W})";
        }

        if (typeof(T) == typeof(Color4))
        {
            var color4 = (Color4)Convert.ChangeType(theValue, typeof(Color4));
            return $"float4({color4.R},{color4.G},{color4.B},{color4.A})";
        }

        if (typeof(T) == typeof(int)) return theValue.ToString();
        if (typeof(T) == typeof(ushort)) return theValue.ToString();

        if (typeof(T) == typeof(Int2))
        {
            var vec2 = (Int2)Convert.ChangeType(theValue, typeof(Int2));
            return $"int2({vec2.X},{vec2.Y})";
        }

        if (typeof(T) == typeof(Int3))
        {
            var vec3 = (Int3)Convert.ChangeType(theValue, typeof(Int3));
            return $"int3({vec3.X},{vec3.Y},{vec3.Z})";
        }

        if (typeof(T) == typeof(Int4))
        {
            var vec4 = (Int4)Convert.ChangeType(theValue, typeof(Int4));
            return $"int4({vec4.X},{vec4.Y},{vec4.Z},{vec4.W})";
        }

        if (typeof(T) == typeof(bool)) return theValue.ToString()?.ToLower();
        if (typeof(T) == typeof(SamplerState)) return "Sampler";
        if (typeof(T) == typeof(Texture)) return "Texture0";

        return GetDefaultForType(typeof(T));
    }

    /// <summary>
    /// Gets the default value string for a type.
    /// </summary>
    public static string GetDefaultForType(Type t) => TypeRegistry.GetDefaultValue(t);

    /// <summary>
    /// Gets the default value string for a type.
    /// </summary>
    public static string GetDefaultForType<T>() => TypeRegistry.GetDefaultValue<T>();

    #endregion

    #region GetSignature

    /// <summary>
    /// Gets the signature name for an AbstractShaderNode (runtime type resolution).
    /// </summary>
    public static string GetSignature(AbstractShaderNode theValue)
    {
        return theValue switch
        {
            ShaderNode<float> => "Float",
            ShaderNode<Vector2> => "Float2",
            ShaderNode<Vector3> => "Float3",
            ShaderNode<Vector4> => "Float4",
            ShaderNode<Color4> => "Float4",
            ShaderNode<bool> => "Bool",
            ShaderNode<int> => "Int",
            ShaderNode<uint> => "UInt",
            ShaderNode<ushort> => "UShort",
            ShaderNode<Int2> => "Int2",
            ShaderNode<Int3> => "Int3",
            ShaderNode<Int4> => "Int4",
            ShaderNode<GpuVoid> => "Void",
            ShaderNode<Matrix> => "Matrix",
            ShaderNode<Matrix3> => "Matrix3",
            ShaderNode<Matrix2> => "Matrix2",
            _ => throw new NotImplementedException("No signature for type: " + theValue.GetType().FullName)
        };
    }

    public static string GetSignature(Type type) => TypeRegistry.GetSignature(type);
    public static string GetSignature<T>() => TypeRegistry.GetSignature<T>();
    public static string GetSignature<T>(ShaderNode<T> shaderNode) => TypeRegistry.GetSignature<T>();

    #endregion

    #region GetGpuType

    /// <summary>
    /// Gets the GPU type name for an AbstractShaderNode (runtime type resolution).
    /// </summary>
    public static string GetGpuType(AbstractShaderNode abstractShaderNode)
    {
        return abstractShaderNode switch
        {
            ShaderNode<float> => "float",
            ShaderNode<Vector2> => "float2",
            ShaderNode<Vector3> => "float3",
            ShaderNode<Vector4> => "float4",
            ShaderNode<Color4> => "float4",
            ShaderNode<Matrix> => "float4x4",
            ShaderNode<Matrix3> => "float3x3",
            ShaderNode<Matrix2> => "float2x2",
            ShaderNode<bool> => "bool",
            ShaderNode<int> => "int",
            ShaderNode<uint> => "uint",
            ShaderNode<ushort> => "ushort",
            ShaderNode<Int2> => "int2",
            ShaderNode<Int3> => "int3",
            ShaderNode<Int4> => "int4",
            ShaderNode<GpuVoid> => "void",
            // GpuArray types
            ShaderNode<GpuArray<float>> => "float",
            ShaderNode<GpuArray<Vector2>> => "float2",
            ShaderNode<GpuArray<Vector3>> => "float3",
            ShaderNode<GpuArray<Vector4>> => "float4",
            ShaderNode<GpuArray<Color4>> => "float4",
            ShaderNode<GpuArray<Matrix>> => "float4x4",
            ShaderNode<GpuArray<Matrix3>> => "float3x3",
            ShaderNode<GpuArray<Matrix2>> => "float2x2",
            ShaderNode<GpuArray<bool>> => "bool",
            ShaderNode<GpuArray<int>> => "int",
            ShaderNode<GpuArray<uint>> => "uint",
            ShaderNode<GpuArray<ushort>> => "ushort",
            ShaderNode<GpuArray<Int2>> => "int2",
            ShaderNode<GpuArray<Int3>> => "int3",
            ShaderNode<GpuArray<Int4>> => "int4",
            _ => throw new NotImplementedException("No GPU type for: " + abstractShaderNode.GetType().FullName)
        };
    }

    public static string GetGpuType(Type type) => TypeRegistry.GetGpuType(type);
    public static string GetGpuType<T>() => TypeRegistry.GetGpuType<T>();
    public static string GetGpuType<T>(ShaderNode<T> shaderNode) => TypeRegistry.GetGpuType<T>();

    #endregion

    #region GetCompositionType

    /// <summary>
    /// Gets the composition type name for an AbstractShaderNode (runtime type resolution).
    /// </summary>
    public static string GetCompositionType(AbstractShaderNode abstractShaderNode)
    {
        return abstractShaderNode switch
        {
            ShaderNode<float> => "ComputeFloat",
            ShaderNode<Vector2> => "ComputeFloat2",
            ShaderNode<Vector3> => "ComputeFloat3",
            ShaderNode<Vector4> => "ComputeFloat4",
            ShaderNode<Color4> => "ComputeColor",
            ShaderNode<Matrix> => "ComputeMatrix",
            ShaderNode<bool> => "ComputeBool",
            ShaderNode<int> => "ComputeInt",
            ShaderNode<uint> => "ComputeUInt",
            ShaderNode<ushort> => "ComputeUShort",
            ShaderNode<Int2> => "ComputeInt2",
            ShaderNode<Int3> => "ComputeInt3",
            ShaderNode<Int4> => "ComputeInt4",
            ShaderNode<GpuVoid> => "ComputeVoid",
            _ => throw new NotImplementedException("No composition type for: " + abstractShaderNode.GetType().FullName)
        };
    }

    public static string GetCompositionType(Type type) => TypeRegistry.GetCompositionType(type);
    public static string GetCompositionType<T>() => TypeRegistry.GetCompositionType<T>();
    public static string GetCompositionType<T>(ShaderNode<T> shaderNode) => TypeRegistry.GetCompositionType<T>();

    #endregion

    #region GetDimension

    /// <summary>
    /// Gets the dimension for an AbstractShaderNode (runtime type resolution).
    /// </summary>
    public static int GetDimension(AbstractShaderNode abstractShaderNode)
    {
        return abstractShaderNode switch
        {
            ShaderNode<float> => 1,
            ShaderNode<Vector2> => 2,
            ShaderNode<Vector3> => 3,
            ShaderNode<Vector4> => 4,
            ShaderNode<Color4> => 4,
            ShaderNode<bool> => 1,
            ShaderNode<int> => 1,
            ShaderNode<uint> => 1,
            ShaderNode<ushort> => 1,
            ShaderNode<Int2> => 2,
            ShaderNode<Int3> => 3,
            ShaderNode<Int4> => 4,
            ShaderNode<GpuVoid> => 0,
            ShaderNode<Matrix> => 16,
            ShaderNode<Matrix3> => 9,
            ShaderNode<Matrix2> => 4,
            _ => 0
        };
    }

    // USED BY VL
    // ReSharper disable once UnusedMember.Global
    public static int GetDimension(Type type) => TypeRegistry.GetDimension(type);
    public static int GetDimension<T>(ShaderNode<T> shaderNode) => TypeRegistry.GetDimension<T>();
    public static int GetDimension<T>() => TypeRegistry.GetDimension<T>();

    public static int GetDimensionFromInt3(Int3 dimension)
    {
        if (dimension is { Y: 1, Z: 1 }) return 1;
        if (dimension.Z == 1) return 2;
        return 3;
    }

    #endregion

    #region GetSizeInBytes

    /// <summary>
    /// Gets the size in bytes for an AbstractShaderNode (runtime type resolution).
    /// </summary>
    public static int GetSizeInBytes(AbstractShaderNode abstractShaderNode)
    {
        return abstractShaderNode switch
        {
            ShaderNode<float> => 4,
            ShaderNode<Vector2> => 8,
            ShaderNode<Vector3> => 12,
            ShaderNode<Vector4> => 16,
            ShaderNode<Color4> => 16,
            ShaderNode<Matrix> => 64,
            ShaderNode<Matrix3> => 36,
            ShaderNode<Matrix2> => 16,
            ShaderNode<bool> => 1,
            ShaderNode<int> => 4,
            ShaderNode<uint> => 4,
            // TODO: BUG - ushort is 2 bytes, not 4. TypeRegistry correctly has ByteSize: 2.
            // Keeping 4 here for GPU alignment compatibility - verify before changing.
            ShaderNode<ushort> => 4,
            ShaderNode<Int2> => 8,
            ShaderNode<Int3> => 12,
            ShaderNode<Int4> => 16,
            // GpuArray types
            ShaderNode<GpuArray<float>> => 4,
            ShaderNode<GpuArray<Vector2>> => 8,
            ShaderNode<GpuArray<Vector3>> => 12,
            ShaderNode<GpuArray<Vector4>> => 16,
            ShaderNode<GpuArray<Color4>> => 16,
            ShaderNode<GpuArray<Matrix>> => 64,
            ShaderNode<GpuArray<Matrix3>> => 36,
            ShaderNode<GpuArray<Matrix2>> => 16,
            ShaderNode<GpuArray<bool>> => 1,
            ShaderNode<GpuArray<int>> => 4,
            ShaderNode<GpuArray<uint>> => 4,
            ShaderNode<GpuArray<ushort>> => 4,
            ShaderNode<GpuArray<Int2>> => 8,
            ShaderNode<GpuArray<Int3>> => 12,
            ShaderNode<GpuArray<Int4>> => 16,
            ShaderNode<GpuVoid> => 0,
            _ => throw new NotImplementedException("No byte size for: " + abstractShaderNode.GetType().FullName)
        };
    }

    public static int GetSizeInBytes(Type type) => TypeRegistry.GetByteSize(type);
    public static int GetSizeInBytes<T>(ShaderNode<T> shaderNode) => TypeRegistry.GetByteSize<T>();
    public static int GetSizeInBytes<T>() => TypeRegistry.GetByteSize<T>();

    #endregion

    #region GetPixelFormat

    /// <summary>
    /// Gets the pixel format for an AbstractShaderNode (runtime type resolution).
    /// </summary>
    public static PixelFormat GetPixelFormat(AbstractShaderNode abstractShaderNode)
    {
        return abstractShaderNode switch
        {
            ShaderNode<float> => PixelFormat.R32_Float,
            ShaderNode<Vector2> => PixelFormat.R32G32_Float,
            ShaderNode<Vector3> => PixelFormat.R32G32B32_Float,
            ShaderNode<Vector4> => PixelFormat.R32G32B32A32_Float,
            ShaderNode<Color4> => PixelFormat.R32G32B32A32_Float,
            ShaderNode<bool> => PixelFormat.R1_UNorm,
            ShaderNode<int> => PixelFormat.R32_SInt,
            ShaderNode<Int2> => PixelFormat.R32G32_SInt,
            ShaderNode<Int3> => PixelFormat.R32G32B32_SInt,
            ShaderNode<Int4> => PixelFormat.R32G32B32A32_SInt,
            ShaderNode<uint> => PixelFormat.R32_UInt,
            ShaderNode<ushort> => PixelFormat.R16_UInt,
            // GpuArray types
            ShaderNode<GpuArray<float>> => PixelFormat.R32_Float,
            ShaderNode<GpuArray<Vector2>> => PixelFormat.R32G32_Float,
            ShaderNode<GpuArray<Vector3>> => PixelFormat.R32G32B32_Float,
            ShaderNode<GpuArray<Vector4>> => PixelFormat.R32G32B32A32_Float,
            ShaderNode<GpuArray<Color4>> => PixelFormat.R32G32B32A32_Float,
            ShaderNode<GpuArray<bool>> => PixelFormat.R1_UNorm,
            ShaderNode<GpuArray<int>> => PixelFormat.R32_SInt,
            ShaderNode<GpuArray<Int2>> => PixelFormat.R32G32_SInt,
            ShaderNode<GpuArray<Int3>> => PixelFormat.R32G32B32_SInt,
            ShaderNode<GpuArray<Int4>> => PixelFormat.R32G32B32A32_SInt,
            ShaderNode<GpuArray<uint>> => PixelFormat.R32_UInt,
            ShaderNode<GpuArray<ushort>> => PixelFormat.R16_UInt,
            _ => throw new NotImplementedException("No pixel format for: " + abstractShaderNode.GetType().FullName)
        };
    }

    public static PixelFormat GetPixelFormat(Type type) => TypeRegistry.GetPixelFormat(type);
    public static PixelFormat GetPixelFormat<T>(ShaderNode<T> shaderNode) => TypeRegistry.GetPixelFormat<T>();
    public static PixelFormat GetPixelFormat<T>() => TypeRegistry.GetPixelFormat<T>();

    #endregion

    #region FromPixelFormat

    /// <summary>
    /// Gets the C# type for a pixel format.
    /// </summary>
    public static Type GetType(PixelFormat format) => TypeRegistry.GetTypeFromPixelFormat(format);

    #endregion

    #region IsIntType

    public static bool IsIntType(Type type) => TypeRegistry.IsIntegerType(type);
    public static bool IsIntType<T>() => TypeRegistry.IsIntegerType<T>();
    public static bool IsIntType<T>(ShaderNode<T> shaderNode) => TypeRegistry.IsIntegerType<T>();

    #endregion
}
