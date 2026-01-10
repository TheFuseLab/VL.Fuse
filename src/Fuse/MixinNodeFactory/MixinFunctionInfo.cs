using System;
using System.Collections.Generic;
using Fuse.function;

namespace Fuse.MixinNodeFactory;

/// <summary>
/// Represents an exported function from a mixin shader.
/// </summary>
public class MixinFunctionInfo
{
    /// <summary>
    /// The function name (e.g., "pows", "fSphere").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The SDSL return type as written in the shader (e.g., "float", "float3").
    /// </summary>
    public string ReturnType { get; set; } = string.Empty;

    /// <summary>
    /// The mapped C# return type (e.g., typeof(float), typeof(Vector3)).
    /// </summary>
    public Type ClrReturnType { get; set; } = typeof(float);

    /// <summary>
    /// The name of the containing mixin shader (e.g., "FuseMathMixin").
    /// </summary>
    public string MixinName { get; set; } = string.Empty;

    /// <summary>
    /// The file path of the source .sdsl file.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// The function parameters.
    /// </summary>
    public List<MixinParameterInfo> Parameters { get; set; } = new();

    /// <summary>
    /// Metadata extracted from comments.
    /// </summary>
    public MixinMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets the input parameters (In modifier only).
    /// </summary>
    public IEnumerable<MixinParameterInfo> InputParameters
    {
        get
        {
            foreach (var param in Parameters)
            {
                if (param.Modifier == InputModifier.In || param.Modifier == InputModifier.InOut)
                    yield return param;
            }
        }
    }

    /// <summary>
    /// Gets the output parameters (Out or InOut modifier).
    /// </summary>
    public IEnumerable<MixinParameterInfo> OutputParameters
    {
        get
        {
            foreach (var param in Parameters)
            {
                if (param.Modifier == InputModifier.Out || param.Modifier == InputModifier.InOut)
                    yield return param;
            }
        }
    }

    /// <summary>
    /// Whether the function has a void return type.
    /// </summary>
    public bool IsVoid => ReturnType == "void";
}
