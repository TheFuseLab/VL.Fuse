using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using StrideBuffer = Stride.Graphics.Buffer;
using StrideSamplerState = Stride.Graphics.SamplerState;
using StrideTexture = Stride.Graphics.Texture;

namespace Fuse;

public sealed class ShaderDiagnosticContext
{
    public string ShaderName { get; init; }
    public string Phase { get; init; }
    public string SourcePath { get; init; }
    public bool IsCompute { get; init; }
    public int CodeLength { get; init; }
    public IReadOnlyList<ShaderStageDiagnostic> Stages { get; init; } = [];
    public IReadOnlyList<string> Mixins { get; init; } = [];
    public IReadOnlyList<ShaderDeclarationDiagnostic> Declarations { get; init; } = [];
    public IReadOnlyList<ShaderInputDiagnostic> Inputs { get; init; } = [];
    public IReadOnlyList<ShaderTimingDiagnostic> Timings { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static ShaderDiagnosticContext Create(
        string shaderName,
        string phase,
        string sourcePath,
        bool isCompute,
        string shaderCode,
        IEnumerable<ShaderStageCompilationDiagnostic> stageCompilations,
        IEnumerable<string> warnings,
        IEnumerable<ShaderTimingDiagnostic> timings = null)
    {
        var stages = stageCompilations?.ToList() ?? [];
        return new ShaderDiagnosticContext
        {
            ShaderName = shaderName,
            Phase = phase,
            SourcePath = sourcePath,
            IsCompute = isCompute,
            CodeLength = shaderCode?.Length ?? 0,
            Stages = stages.Select(s => s.Stage).ToList(),
            Mixins = stages.SelectMany(s => s.Result.Mixins).Distinct().OrderBy(s => s).ToList(),
            Declarations = stages.SelectMany(s => s.Result.Declarations)
                .Select(ShaderDeclarationDiagnostic.FromDeclaration)
                .DistinctBy(d => d.InputName + "|" + d.TypeName + "|" + d.ComputeTypeName + "|" + d.IsResource)
                .OrderBy(d => d.IsResource ? 0 : 1)
                .ThenBy(d => d.InputName)
                .ToList(),
            Inputs = stages.SelectMany(s => s.Result.Inputs)
                .Select(ShaderInputDiagnostic.FromInput)
                .DistinctBy(i => i.Id)
                .OrderBy(i => i.Id)
                .ToList(),
            Timings = timings?.ToList() ?? [],
            Warnings = warnings?.Where(w => !string.IsNullOrWhiteSpace(w)).ToList() ?? []
        };
    }

    public string ToDiagnosticLog()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Timestamp: {DateTime.Now:O}");
        builder.AppendLine($"Shader: {ShaderName ?? "<unknown>"}");
        builder.AppendLine($"Phase: {Phase ?? "<unknown>"}");
        builder.AppendLine($"SourcePath: {SourcePath ?? "<none>"}");
        builder.AppendLine($"IsCompute: {IsCompute}");
        builder.AppendLine($"CodeLength: {CodeLength}");

        builder.AppendLine("Stages:");
        AppendList(builder, Stages, s => $"  - Key={s.Key}; Root={s.RootNodeId}; Type={s.RootNodeType}");

        builder.AppendLine("Mixins:");
        AppendList(builder, Mixins, s => $"  - {s}");

        builder.AppendLine("Declarations:");
        AppendList(builder, Declarations,
            d => $"  - Name={d.InputName}; Resource={d.IsResource}; Type={d.TypeName}; ComputeType={d.ComputeTypeName}");

        builder.AppendLine("Inputs:");
        AppendList(builder, Inputs,
            i => $"  - Id={i.Id}; Name={i.Name}; Type={i.NodeType}; Value={i.ValueDescription}");

        builder.AppendLine("Timings:");
        AppendList(builder, Timings,
            t => $"  - {t.Name}: {t.ElapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)} ms");

        builder.AppendLine("Warnings:");
        AppendList(builder, Warnings, w => $"  - {w}");

        return builder.ToString();
    }

    private static void AppendList<T>(StringBuilder builder, IReadOnlyCollection<T> values, Func<T, string> formatter)
    {
        if (values == null || values.Count == 0)
        {
            builder.AppendLine("  <none>");
            return;
        }

        foreach (var value in values)
            builder.AppendLine(formatter(value));
    }
}

public readonly record struct ShaderStageCompilationDiagnostic(
    ShaderStageDiagnostic Stage,
    ShaderCompilationResult Result);

public sealed record ShaderTimingDiagnostic(string Name, double ElapsedMilliseconds);

public sealed record ShaderStageDiagnostic(string Key, string RootNodeId, string RootNodeType)
{
    public static ShaderStageDiagnostic FromStage(string key, AbstractShaderNode node)
    {
        return new ShaderStageDiagnostic(
            key ?? "<unknown>",
            node?.ID ?? "<null>",
            node?.GetType().FullName ?? "<null>");
    }
}

public sealed record ShaderDeclarationDiagnostic(
    string InputName,
    string TypeName,
    string ComputeTypeName,
    bool IsResource)
{
    public static ShaderDeclarationDiagnostic FromDeclaration(FieldDeclaration declaration)
    {
        return new ShaderDeclarationDiagnostic(
            declaration?.InputName ?? "<unknown>",
            declaration?.TypeName ?? "<unknown>",
            declaration?.ComputeTypeName ?? "<unknown>",
            declaration?.IsResource ?? false);
    }
}

public sealed record ShaderInputDiagnostic(
    string Id,
    string Name,
    string NodeType,
    string ValueDescription)
{
    public static ShaderInputDiagnostic FromInput(IGpuInput input)
    {
        var node = input as AbstractShaderNode;
        return new ShaderInputDiagnostic(
            node?.ID ?? "<unknown>",
            node?.Name ?? "<unknown>",
            input?.GetType().FullName ?? "<unknown>",
            DescribeInputValue(input));
    }

    private static string DescribeInputValue(IGpuInput input)
    {
        if (input == null)
            return "<null input>";

        var valueProperty = input.GetType().GetProperty("Value");
        if (valueProperty == null)
            return "<no value>";

        object value;
        try
        {
            value = valueProperty.GetValue(input);
        }
        catch (Exception ex)
        {
            return $"<value read failed: {ex.GetType().Name}: {ex.Message}>";
        }

        return value switch
        {
            null => "<null>",
            StrideBuffer buffer => DescribeBuffer(buffer),
            StrideTexture texture => DescribeTexture(texture),
            StrideSamplerState sampler => $"SamplerState; IsDisposed={sampler.IsDisposed}",
            _ => value.ToString()
        };
    }

    private static string DescribeBuffer(StrideBuffer buffer)
    {
        if (buffer == null)
            return "<null buffer>";

        return
            $"Buffer; IsDisposed={buffer.IsDisposed}; ElementCount={buffer.ElementCount}; Size={buffer.Description.SizeInBytes}; Stride={buffer.Description.StructureByteStride}; Flags={buffer.Description.BufferFlags}";
    }

    private static string DescribeTexture(StrideTexture texture)
    {
        if (texture == null)
            return "<null texture>";

        return
            $"Texture; IsDisposed={texture.IsDisposed}; Dimension={texture.Dimension}; Size={texture.Width}x{texture.Height}x{texture.Depth}; ArraySize={texture.ArraySize}; Format={texture.Format}; MipLevels={texture.MipLevels}; ViewType={texture.ViewType}";
    }
}
