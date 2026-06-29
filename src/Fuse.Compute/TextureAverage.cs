using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.Import;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute;

[ProcessNode(Name = "Average", Category = "Fuse.Compute.Texture", FragmentSelection = FragmentSelection.Explicit)]
public class Average<TIndex, T> : TextureNeighborhoodNode<TIndex, T> where T : struct
{
    private readonly ShaderNode<int> _radius;

    public Average(
        NodeContext nodeContext,
        ITextureInputProvider texture,
        ShaderNode<int> radius,
        ShaderNode<TIndex> index,
        ShaderNode<T> theDefault = null)
        : base(nodeContext, "Average", texture, index, theDefault)
    {
        _radius = radius;
        RefreshInputs();
    }

    protected override IEnumerable<AbstractShaderNode> GetAdditionalInputs()
    {
        return new AbstractShaderNode[] { _radius };
    }

    protected override string SourceTemplate()
    {
        if (_radius == null || !TryGetTextureReference(out var textureName, out var indexId, out _))
            return GenerateDefaultSource();

        var radiusName = "averageRadius_${resultName}";
        var countName = "averageCount_${resultName}";
        var loopSource = EmitNestedRadiusLoop(
            "average",
            radiusName,
            "${resultName} += ${textureName}[${index} + ${offset}];\n${countName}++;",
            new Dictionary<string, string>
            {
                { "textureName", textureName },
                { "index", indexId },
                { "countName", countName }
            });
        var shaderCode = @"
${resultType} ${resultName} = ${zero};
int ${radiusName} = max(0, ${radius});
int ${countName} = 0;
${loops}
${resultName} = ${resultName} / max(1.0, (float)${countName});";

        return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
        {
            { "textureName", textureName },
            { "index", indexId },
            { "radius", _radius.ID },
            { "radiusName", radiusName },
            { "countName", countName },
            { "zero", TypeHelpers.GetDefaultForType<T>() },
            { "loops", loopSource }
        });
    }
}
