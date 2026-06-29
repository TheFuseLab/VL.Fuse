using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute;

[ProcessNode(Name = "Laplace2D (8 Karl Sims)", Category = "Fuse.Compute.Texture", FragmentSelection = FragmentSelection.Explicit)]
public class Laplace2DKarlSims<T> : TextureNeighborhoodNode<Int2, T> where T : struct
{
    private readonly ShaderNode<float> _adjacentWeight;
    private readonly ShaderNode<float> _centerWeight;
    private readonly ShaderNode<float> _diagonalWeight;

    public Laplace2DKarlSims(
        NodeContext nodeContext,
        ITextureInputProvider input,
        ShaderNode<float> adjacentWeight = null,
        ShaderNode<float> diagonalWeight = null,
        ShaderNode<float> centerWeight = null,
        ShaderNode<T> theDefault = null)
        : base(nodeContext, "Laplace2DKarlSims", input, CreateDispatchIndex(nodeContext), theDefault)
    {
        _adjacentWeight = adjacentWeight ?? new ConstantValue<float>(0.2f);
        _diagonalWeight = diagonalWeight ?? new ConstantValue<float>(0.05f);
        _centerWeight = centerWeight ?? new ConstantValue<float>(1f);
        RefreshInputs();
    }

    protected override IEnumerable<AbstractShaderNode> GetAdditionalInputs()
    {
        return new AbstractShaderNode[] { _adjacentWeight, _diagonalWeight, _centerWeight };
    }

    protected override string SourceTemplate()
    {
        if (!TryGetTextureReference(out var textureName, out var indexId, out _))
            return GenerateDefaultSource();

        var adjacentTerms = ShaderNodesUtil.Evaluate(
            EmitUnrolledWeightedSampleSum(TextureNeighborhood.AxisOffsets(2), "${resultName}", _adjacentWeight.ID),
            new Dictionary<string, string>
            {
                { "textureName", textureName },
                { "index", indexId }
            });
        var diagonalTerms = ShaderNodesUtil.Evaluate(
            EmitUnrolledWeightedSampleSum(TextureNeighborhood.DiagonalOffsets(2), "${resultName}", _diagonalWeight.ID),
            new Dictionary<string, string>
            {
                { "textureName", textureName },
                { "index", indexId }
            });
        var shaderCode = @"
${resultType} laplaceCenter_${resultName} = ${textureName}[${index}];
${resultType} ${resultName} = ${zero};
${adjacentTerms}
${diagonalTerms}
${resultName} -= laplaceCenter_${resultName} * ${centerWeight};";

        return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
        {
            { "textureName", textureName },
            { "index", indexId },
            { "zero", TypeHelpers.GetDefaultForType<T>() },
            { "adjacentTerms", adjacentTerms },
            { "diagonalTerms", diagonalTerms },
            { "centerWeight", _centerWeight.ID }
        });
    }

    private static ShaderNode<Int2> CreateDispatchIndex(NodeContext nodeContext)
    {
        var dynamicIndex = new DynamicIndex(nodeContext, new DispatchIdIndexProvider());
        return new Fuse.GetMember<Int3, Int2>(nodeContext, dynamicIndex, "xy");
    }
}
