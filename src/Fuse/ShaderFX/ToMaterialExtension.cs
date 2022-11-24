using System.Collections.Generic;
using System.Text;
using Fuse.compute;
using Stride.Engine;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{
    public enum InputPrimitiveType
    {
        Point, //point,1
        Line, //line,2
        Triangle, //triangle,3
        LineAdjacency, // lineadj,4
        TriangleAdjacency //triangleadj,6
    }

    public enum OutputPrimitiveType
    {
        Point, //PointStream
        Line, //LineStream
        Triangle //TriangleStream
    }
    
    public class ToMaterialExtension<T> : AbstractToShaderFX<T> 
    {

        private const string ShaderSource = @"
shader ${shaderID} : MaterialExtension${mixins}
{
    rgroup PerMaterial{
${groupDeclarations}
    }

    cbuffer PerMaterial{
${declarations}
    }

${structs}

${streams}

${functions}

${compositions}

    override ${resultType} Compute()
    {
${sourceFX}
        ${resultFX}
    }

    [maxvertexcount(${numVertex})]
	stage void GSMain(${intputType} Input input[${numElements}], inout ${outputType}<Output> outputStream)
	{
		    
${sourceFX}
        ${resultFX}
	}

    //override shading, create sphere impostor in this case
	stage override float4 Shading()
	{
		float2 mapping = streams.TexCoord;
		float lenSqr = dot(mapping, mapping);
		if (lenSqr > 1)
			discard; // Circles
		
		float z = sqrt(1 - lenSqr);

		float3 normal = float3(mapping, z);
		normal = normalize(mul(float4(normal, 0), ViewInverse).xyz);
		
		// new code to write pos & depth
		// Shadows look correct now, but if you move the camera a distance away have other weirdness
		// /*
		float4 worldPos = float4((normal * streams.sphereSize) + streams.PositionWS.xyz, 1);
		// */

		return StrideShadingWorldNormal(worldPos, normal);
	}
};";

        
        
        
        public ToMaterialExtension(ShaderNode<T> theCompute, bool theIsCompute = false, string theShaderSource = ShaderSource) : base( 
            theCompute, 
            new List<string>(),
            new Dictionary<string, string>(),
            theIsCompute,
            theShaderSource)
        {
        }

       
    }
    
    public class AppendStreams : SimpleKeyword
    {
    
        public AppendStreams(NodeContext nodeContext) : base( nodeContext, "outputStream.Append(streams);")
        {
        }
    }
    
    public class RestartStrip : SimpleKeyword
    {
    
        public RestartStrip(NodeContext nodeContext) : base( nodeContext, "outputStream.RestartStrip();")
        {
        }
    }
    
    public class SetStreams : ShaderNode<GpuVoid> , IComputeVoid
    {
        private ShaderNode<int> _index;
        
        public SetStreams(NodeContext nodeContext) : base( nodeContext, "getStream")
        {
            
        }

        public void SetInputs(ShaderNode<int> theIndex)
        {
            _index = theIndex;
            
            SetInputs(new List<AbstractShaderNode>{theIndex});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "streams = input[${index}];";
            
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"index", _index.ID}
            });
        }
    }
}