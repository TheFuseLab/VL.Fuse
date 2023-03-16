using System;
using System.Collections.Generic;
using Fuse.compute;
using Fuse.ShaderFX;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
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
    
    public class GeometryStage : AbstractStage
    {
        private const string ShaderSource = @"
    [maxvertexcount(${numVertex})]
	stage void GSMain(${inputType} Input input[${numElements}], inout ${outputType}<Output> outputStream)
	{
        ${sourceGS}
	}";
        
        private readonly int _myMaximumVertexCount;
        private readonly InputPrimitiveType _myInputPrimitiveType;
        private readonly OutputPrimitiveType _myOutputPrimitiveType;

        public GeometryStage(
            ShaderNode<GpuVoid> theGeometryNode,
            int theMaximumVertexCount, 
            InputPrimitiveType theInputPrimitiveType,
            OutputPrimitiveType theOutputPrimitive) : base ("GS", theGeometryNode)
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
        }

        public override string Source()
        {
            return ShaderSource;
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
        private readonly ShaderNode<int> _index;
        
        public SetStreams(NodeContext nodeContext, ShaderNode<int> theIndex) : base( nodeContext, "getStream")
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
    
    public class GetStreamMember<T> : ShaderNode<T>
    {
        private readonly ShaderNode<int> _index;
        private readonly string _member;
        
        public GetStreamMember(NodeContext nodeContext, ShaderNode<int> theIndex, string theMember) : base( nodeContext, "getStream")
        {
            _index = theIndex;
            _member = theMember;
            
            SetInputs(new List<AbstractShaderNode>{theIndex});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${resultType} ${resultName} = input[${index}].${member};";
            
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"index", _index.ID},
                {"member", _member}
            });
        }
    }
}