using System;
using Fuse.function;

namespace Fuse.MixinNodeFactory;

/// <summary>
/// Represents a parameter of an exported mixin function.
/// </summary>
public class MixinParameterInfo
{
    /// <summary>
    /// The parameter name (e.g., "r").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The SDSL type as written in the shader (e.g., "float", "float3").
    /// </summary>
    public string SdslType { get; set; } = string.Empty;

    /// <summary>
    /// The mapped C# type (e.g., typeof(float), typeof(Vector3)).
    /// </summary>
    public Type ClrType { get; set; } = typeof(float);

    /// <summary>
    /// The parameter modifier (In, Out, InOut).
    /// </summary>
    public InputModifier Modifier { get; set; } = InputModifier.In;

    /// <summary>
    /// Description from @param comment.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Default value from @default comment or inline default.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Whether this parameter has an inline default value in the function signature.
    /// </summary>
    public bool HasInlineDefault { get; set; }

    /// <summary>
    /// The inline default value as written in the shader (e.g., "1.0", "float3(0,0,0)").
    /// </summary>
    public string? InlineDefaultString { get; set; }

    /// <summary>
    /// For buffer types, the element type (e.g., float for StructuredBuffer&lt;float&gt;).
    /// </summary>
    public Type? BufferElementType { get; set; }

    /// <summary>
    /// Whether this is a read-write resource (RWBuffer, RWTexture, etc.).
    /// </summary>
    public bool IsReadWrite { get; set; }

    /// <summary>
    /// Whether this parameter is an array type.
    /// </summary>
    public bool IsArray { get; set; }

    /// <summary>
    /// For array types, the fixed array size (e.g., 4 for float4[4]).
    /// </summary>
    public int ArraySize { get; set; }
}
