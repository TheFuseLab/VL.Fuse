using System;
using System.Collections.Generic;
using System.Text;
using Fuse.compute;
using Stride.Core.Mathematics;
using Stride.Engine;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;
using Vortice.Vulkan;

namespace Fuse.ShaderFX
{
    
    
    public class ToMaterialExtension : AbstractToShaderFX<GpuVoid> 
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

${constantArrays}

${structs}

${streams}

${functions}

${compositions}

    [maxvertexcount(${numVertex})]
	stage void GSMain(${inputType} Input input[${numElements}], inout ${outputType}<Output> outputStream)
	{
        ${sourceGS}
	}

    //override shading, create sphere impostor in this case
	stage override float4 Shading()
	{
        ${sourceSWN}
		return ${resultSWN};
	}
};";


        private readonly int _myMaximumVertexCount;
        private readonly InputPrimitiveType _myInputPrimitiveType;
        private readonly OutputPrimitiveType _myOutputPrimitiveType;
        
        public ToMaterialExtension(
            ShaderNode<GpuVoid> theGeometryNode,
            ShaderNode<Vector4> theShading, 
            int theMaximumVertexCount, 
            InputPrimitiveType theInputPrimitiveType,
            OutputPrimitiveType theOutputPrimitive
            ) : base( 
            new Dictionary<string, AbstractShaderNode>
            {
                {"GS", theGeometryNode},
                {"SWN", theShading}
            }, 
            new List<string>(),
            new Dictionary<string, string>(),
            false,
            ShaderSource)
        {
            _myMaximumVertexCount = theMaximumVertexCount;
            _myInputPrimitiveType = theInputPrimitiveType;
            _myOutputPrimitiveType = theOutputPrimitive;
        }

        private string InputPrimitiveTypeString()
        {
            return _myInputPrimitiveType switch
            {
                InputPrimitiveType.Point => "point",
                InputPrimitiveType.Line => "line",
                InputPrimitiveType.Triangle => "triangle",
                InputPrimitiveType.LineAdjacency => "lineadj",
                InputPrimitiveType.TriangleAdjacency => "triangleadj",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        private string InputPrimitiveTypeNumElements()
        {
            return _myInputPrimitiveType switch
            {
                InputPrimitiveType.Point => "1",
                InputPrimitiveType.Line => "2",
                InputPrimitiveType.Triangle => "3",
                InputPrimitiveType.LineAdjacency => "4",
                InputPrimitiveType.TriangleAdjacency => "6",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        private string OutputPrimitiveTypeString()
        {
            return _myOutputPrimitiveType switch
            {
                OutputPrimitiveType.Point => "PointStream",
                OutputPrimitiveType.Line => "LineStream",
                OutputPrimitiveType.Triangle => "TriangleStream",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public override void AppendInputs(Dictionary<string, string> theTemplateMap)
        {
            theTemplateMap["numVertex"] = _myMaximumVertexCount.ToString();
            theTemplateMap["inputType"] = InputPrimitiveTypeString();
            theTemplateMap["numElements"] = InputPrimitiveTypeNumElements();
            theTemplateMap["outputType"] = OutputPrimitiveTypeString();
            theTemplateMap["resultSWN"] = Inputs["SWN"].ID +";";
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