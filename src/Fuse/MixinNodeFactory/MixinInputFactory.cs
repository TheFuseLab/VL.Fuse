using System;
using System.Collections.Generic;
using Fuse.compute;
using Fuse.function;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse.MixinNodeFactory;

/// <summary>
/// Helper for determining input types and default values for mixin function parameters.
/// This provides metadata that VL can use to create appropriate input nodes.
/// </summary>
public static class MixinInputFactory
{
    /// <summary>
    /// Information about how to create an input for a parameter.
    /// </summary>
    public class InputInfo
    {
        /// <summary>
        /// The parameter name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The C# type for the input.
        /// </summary>
        public Type ClrType { get; set; } = typeof(float);

        /// <summary>
        /// The default value for the input.
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// Description from comments.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Whether this is an output parameter (Out or InOut).
        /// </summary>
        public bool IsOutput { get; set; }

        /// <summary>
        /// Whether this is also an input (In or InOut).
        /// </summary>
        public bool IsInput { get; set; }

        /// <summary>
        /// The input modifier.
        /// </summary>
        public InputModifier Modifier { get; set; }

        /// <summary>
        /// Whether this is a texture input.
        /// </summary>
        public bool IsTexture => ClrType == typeof(Texture);

        /// <summary>
        /// Whether this is a buffer input.
        /// </summary>
        public bool IsBuffer => ClrType == typeof(Buffer);

        /// <summary>
        /// Whether this is a sampler input.
        /// </summary>
        public bool IsSampler => ClrType == typeof(SamplerState);

        /// <summary>
        /// Whether this is a simple value input (not texture/buffer/sampler).
        /// </summary>
        public bool IsValueInput => !IsTexture && !IsBuffer && !IsSampler;
    }

    /// <summary>
    /// Gets input information for all parameters of a function.
    /// </summary>
    /// <param name="functionInfo">The function information.</param>
    /// <returns>List of input info for each parameter.</returns>
    public static List<InputInfo> GetInputInfo(MixinFunctionInfo functionInfo)
    {
        var result = new List<InputInfo>();

        foreach (var param in functionInfo.Parameters)
        {
            result.Add(new InputInfo
            {
                Name = param.Name,
                ClrType = param.ClrType,
                DefaultValue = param.DefaultValue,
                Description = param.Description,
                Modifier = param.Modifier,
                IsInput = param.Modifier == InputModifier.In || param.Modifier == InputModifier.InOut,
                IsOutput = param.Modifier == InputModifier.Out || param.Modifier == InputModifier.InOut
            });
        }

        return result;
    }

    /// <summary>
    /// Gets only the input parameters (In and InOut modifiers).
    /// </summary>
    public static List<InputInfo> GetInputOnlyInfo(MixinFunctionInfo functionInfo)
    {
        var result = new List<InputInfo>();

        foreach (var param in functionInfo.Parameters)
        {
            if (param.Modifier == InputModifier.In || param.Modifier == InputModifier.InOut)
            {
                result.Add(new InputInfo
                {
                    Name = param.Name,
                    ClrType = param.ClrType,
                    DefaultValue = param.DefaultValue,
                    Description = param.Description,
                    Modifier = param.Modifier,
                    IsInput = true,
                    IsOutput = param.Modifier == InputModifier.InOut
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Gets only the output parameters (Out and InOut modifiers).
    /// </summary>
    public static List<InputInfo> GetOutputOnlyInfo(MixinFunctionInfo functionInfo)
    {
        var result = new List<InputInfo>();

        foreach (var param in functionInfo.Parameters)
        {
            if (param.Modifier == InputModifier.Out || param.Modifier == InputModifier.InOut)
            {
                result.Add(new InputInfo
                {
                    Name = param.Name,
                    ClrType = param.ClrType,
                    DefaultValue = param.DefaultValue,
                    Description = param.Description,
                    Modifier = param.Modifier,
                    IsInput = param.Modifier == InputModifier.InOut,
                    IsOutput = true
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Gets output info including the return type.
    /// </summary>
    public static List<InputInfo> GetAllOutputInfo(MixinFunctionInfo functionInfo)
    {
        var result = new List<InputInfo>();

        // Add return type as first output (if not void)
        if (!functionInfo.IsVoid)
        {
            result.Add(new InputInfo
            {
                Name = "Result",
                ClrType = functionInfo.ClrReturnType,
                DefaultValue = SdslTypeMapper.GetDefaultValue(functionInfo.ClrReturnType),
                Description = functionInfo.Metadata.Summary,
                IsInput = false,
                IsOutput = true,
                Modifier = InputModifier.In // N/A for return
            });
        }

        // Add out/inout parameters
        result.AddRange(GetOutputOnlyInfo(functionInfo));

        return result;
    }

    /// <summary>
    /// Gets the VL-friendly type name for a parameter type.
    /// </summary>
    public static string GetVLTypeName(Type clrType)
    {
        if (clrType == typeof(float)) return "Float32";
        if (clrType == typeof(Vector2)) return "Vector2";
        if (clrType == typeof(Vector3)) return "Vector3";
        if (clrType == typeof(Vector4)) return "Vector4";
        if (clrType == typeof(int)) return "Integer32";
        if (clrType == typeof(Int2)) return "Int2";
        if (clrType == typeof(Int3)) return "Int3";
        if (clrType == typeof(Int4)) return "Int4";
        if (clrType == typeof(uint)) return "UInteger32";
        if (clrType == typeof(bool)) return "Boolean";
        if (clrType == typeof(Matrix)) return "Matrix";
        if (clrType == typeof(Texture)) return "Texture";
        if (clrType == typeof(SamplerState)) return "SamplerState";
        if (clrType == typeof(Buffer)) return "Buffer";
        if (clrType == typeof(GpuVoid)) return "Void";

        return clrType.Name;
    }
}
