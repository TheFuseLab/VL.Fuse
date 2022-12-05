using System.Collections.Generic;
using System.Text;
using Fuse.compute;
using Stride.Core.Mathematics;
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

    [maxvertexcount(${numVertex})]
	stage void GSMain(${intputType} Input input[${numElements}], inout ${outputType}<Output> outputStream)
	{
		    
        ${sourceGS}
        ${resultGS}
	}

    //override shading, create sphere impostor in this case
	stage override float4 Shading()
	{
		

        ${sourceN}
		float3 normal = ${resultN};
		 
        ${sourceWP}
		float4 worldPos = ${resultWP};

		return StrideShadingWorldNormal(worldPos, normal);
	}
};";

        
        
        
        public ToMaterialExtension(ShaderNode<GpuVoid> theGeometryNode, ShaderNode<Vector3> theNormalNode, ShaderNode<Vector4> theWorldPosNode, bool theIsCompute = false, string theShaderSource = ShaderSource) : base( 
            new Dictionary<string, AbstractShaderNode>
            {
                {"GS", theGeometryNode},
                {"N", theNormalNode},
                {"WP", theWorldPosNode}
            }, 
            new List<string>(),
            new Dictionary<string, string>(),
            theIsCompute,
            theShaderSource)
        {
        }


        public override void AppendInputs(Dictionary<string, string> theTemplateMap)
        {
            theTemplateMap["resultGS"] = "";
            theTemplateMap["resultN"] = Inputs["N"].ID +";";
            theTemplateMap["resultWP"] = Inputs["WP"].ID +";";
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