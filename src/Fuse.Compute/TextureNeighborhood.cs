using System;
using System.Collections.Generic;
using System.Linq;
using Stride.Core.Mathematics;
using Stride.Rendering.Materials;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute;

public readonly record struct TextureNeighborOffset(int X, int Y, int Z = 0)
{
    public string ToShaderOffset(int dimension)
    {
        return dimension switch
        {
            1 => X.ToString(),
            2 => $"int2({X}, {Y})",
            3 => $"int3({X}, {Y}, {Z})",
            _ => "0"
        };
    }
}

internal static class TextureNeighborhood
{
    public static ITextureInput ResolveTextureInput(ITextureInputProvider textureProvider)
    {
        var texture = textureProvider?.GetTextureInput();
        return texture is ITextureAttribute { TextureInput: not null } textureAttribute
            ? textureAttribute.TextureInput
            : texture;
    }

    public static AbstractShaderNode GetTextureDeclarationInput(ITextureInput texture)
    {
        return texture is ITextureAttribute { TextureInput: null }
            ? null
            : texture as AbstractShaderNode;
    }

    public static int GetIndexDimension<TIndex>()
    {
        if (typeof(TIndex) == typeof(int)) return 1;
        if (typeof(TIndex) == typeof(Int2)) return 2;
        if (typeof(TIndex) == typeof(Int3)) return 3;
        return 0;
    }

    public static IReadOnlyList<TextureNeighborOffset> AxisOffsets(int dimension)
    {
        return dimension switch
        {
            1 => new[] { new TextureNeighborOffset(-1, 0), new TextureNeighborOffset(1, 0) },
            2 => new[]
            {
                new TextureNeighborOffset(-1, 0),
                new TextureNeighborOffset(1, 0),
                new TextureNeighborOffset(0, -1),
                new TextureNeighborOffset(0, 1)
            },
            3 => new[]
            {
                new TextureNeighborOffset(-1, 0, 0),
                new TextureNeighborOffset(1, 0, 0),
                new TextureNeighborOffset(0, -1, 0),
                new TextureNeighborOffset(0, 1, 0),
                new TextureNeighborOffset(0, 0, -1),
                new TextureNeighborOffset(0, 0, 1)
            },
            _ => Array.Empty<TextureNeighborOffset>()
        };
    }

    public static IReadOnlyList<TextureNeighborOffset> DiagonalOffsets(int dimension)
    {
        if (dimension < 2 || dimension > 3)
            return Array.Empty<TextureNeighborOffset>();

        var values = new[] { -1, 0, 1 };
        return Enumerable
            .Range(0, (int)System.Math.Pow(3, dimension))
            .Select(i => ToOffset(i, dimension, values))
            .Where(offset =>
            {
                var active = System.Math.Abs(offset.X) + System.Math.Abs(offset.Y) + System.Math.Abs(offset.Z);
                return active >= 2;
            })
            .ToArray();
    }

    private static TextureNeighborOffset ToOffset(int index, int dimension, int[] values)
    {
        var x = values[index % 3];
        var y = dimension >= 2 ? values[index / 3 % 3] : 0;
        var z = dimension >= 3 ? values[index / 9 % 3] : 0;
        return new TextureNeighborOffset(x, y, z);
    }
}

public abstract class TextureNeighborhoodNode<TIndex, T> : ShaderNode<T> where T : struct
{
    private readonly ITextureInputProvider _textureProvider;

    protected TextureNeighborhoodNode(
        NodeContext nodeContext,
        string name,
        ITextureInputProvider textureProvider,
        ShaderNode<TIndex> index,
        ShaderNode<T> defaultValue = null)
        : base(nodeContext, name, defaultValue)
    {
        _textureProvider = textureProvider;
        Index = index;
        RefreshTexture();
    }

    protected ShaderNode<TIndex> Index { get; }

    protected ITextureInput Texture { get; private set; }

    protected int NeighborhoodDimension => TextureNeighborhood.GetIndexDimension<TIndex>();

    public override void OnPassContext(ShaderGeneratorContext nodeContext)
    {
        RefreshInputs();
        base.OnPassContext(nodeContext);
    }

    protected void RefreshInputs()
    {
        RefreshTexture();
        SetInputs(CreateInputs());
    }

    protected virtual IEnumerable<AbstractShaderNode> GetAdditionalInputs()
    {
        return Array.Empty<AbstractShaderNode>();
    }

    protected bool TryGetTextureReference(out string textureName, out string indexId, out int dimension)
    {
        RefreshTexture();
        textureName = Texture?.TextureID();
        indexId = Index?.ID;
        dimension = NeighborhoodDimension;
        return !string.IsNullOrWhiteSpace(textureName)
               && !string.IsNullOrWhiteSpace(indexId)
               && dimension > 0;
    }

    protected string EmitUnrolledWeightedSampleSum(
        IEnumerable<TextureNeighborOffset> offsets,
        string target,
        string weight)
    {
        return string.Join(
            "\n",
            offsets.Select(offset =>
                $"{target} += {Texture.TextureID()}[{Index.ID} + {offset.ToShaderOffset(NeighborhoodDimension)}] * {weight};"));
    }

    protected string EmitNestedRadiusLoop(
        string variablePrefix,
        string radiusName,
        string bodyTemplate,
        IReadOnlyDictionary<string, string> replacements = null)
    {
        var offset = NeighborhoodDimension switch
        {
            1 => $"{variablePrefix}X_${{resultName}}",
            2 => $"int2({variablePrefix}X_${{resultName}}, {variablePrefix}Y_${{resultName}})",
            3 => $"int3({variablePrefix}X_${{resultName}}, {variablePrefix}Y_${{resultName}}, {variablePrefix}Z_${{resultName}})",
            _ => "0"
        };
        var body = ShaderNodesUtil.Evaluate(
            bodyTemplate,
            Merge(replacements, new Dictionary<string, string> { { "offset", offset } }));

        return NeighborhoodDimension switch
        {
            1 => $@"
for (int {variablePrefix}X_${{resultName}} = -{radiusName}; {variablePrefix}X_${{resultName}} <= {radiusName}; {variablePrefix}X_${{resultName}}++)
{{
    {body}
}}",
            2 => $@"
for (int {variablePrefix}Y_${{resultName}} = -{radiusName}; {variablePrefix}Y_${{resultName}} <= {radiusName}; {variablePrefix}Y_${{resultName}}++)
{{
    for (int {variablePrefix}X_${{resultName}} = -{radiusName}; {variablePrefix}X_${{resultName}} <= {radiusName}; {variablePrefix}X_${{resultName}}++)
    {{
        {body}
    }}
}}",
            3 => $@"
for (int {variablePrefix}Z_${{resultName}} = -{radiusName}; {variablePrefix}Z_${{resultName}} <= {radiusName}; {variablePrefix}Z_${{resultName}}++)
{{
    for (int {variablePrefix}Y_${{resultName}} = -{radiusName}; {variablePrefix}Y_${{resultName}} <= {radiusName}; {variablePrefix}Y_${{resultName}}++)
    {{
        for (int {variablePrefix}X_${{resultName}} = -{radiusName}; {variablePrefix}X_${{resultName}} <= {radiusName}; {variablePrefix}X_${{resultName}}++)
        {{
            {body}
        }}
    }}
}}",
            _ => ""
        };
    }

    protected string EmitOffsetBufferLoop(
        string variablePrefix,
        string offsetBufferId,
        string offsetCountId,
        string bodyTemplate,
        IReadOnlyDictionary<string, string> replacements = null)
    {
        var iterator = $"{variablePrefix}OffsetIndex_${{resultName}}";
        var offset = $"{variablePrefix}Offset_${{resultName}}";
        var body = ShaderNodesUtil.Evaluate(
            bodyTemplate,
            Merge(replacements, new Dictionary<string, string> { { "offset", offset } }));

        return $@"
for (int {iterator} = 0; {iterator} < {offsetCountId}; {iterator}++)
{{
    {TypeHelpers.GetGpuType<TIndex>()} {offset} = {offsetBufferId}[{iterator}];
    {body}
}}";
    }

    private IEnumerable<AbstractShaderNode> CreateInputs()
    {
        yield return TextureNeighborhood.GetTextureDeclarationInput(Texture);
        yield return Index;

        foreach (var input in GetAdditionalInputs())
            yield return input;
    }

    private void RefreshTexture()
    {
        Texture = TextureNeighborhood.ResolveTextureInput(_textureProvider);
    }

    private static Dictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> replacements,
        IReadOnlyDictionary<string, string> additional)
    {
        var result = replacements == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(replacements);

        foreach (var pair in additional)
            result[pair.Key] = pair.Value;

        return result;
    }
}
