using System;
using System.Collections.Generic;
using Fuse.compute;
using Stride.Core.Mathematics;

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
    
    public class GeometryStage
    {
        private readonly int _myMaximumVertexCount;
        private readonly InputPrimitiveType _myInputPrimitiveType;
        private readonly OutputPrimitiveType _myOutputPrimitiveType;
        
        public ShaderNode<GpuVoid> GeometryNode { get; set; }

        public GeometryStage(
            ShaderNode<GpuVoid> theGeometryNode,
            int theMaximumVertexCount, 
            InputPrimitiveType theInputPrimitiveType,
            OutputPrimitiveType theOutputPrimitive)
        {
            GeometryNode = theGeometryNode;
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
        
        public void AppendInputs(Dictionary<string, string> theTemplateMap)
        {
            theTemplateMap["numVertex"] = _myMaximumVertexCount.ToString();
            theTemplateMap["inputType"] = InputPrimitiveTypeString();
            theTemplateMap["numElements"] = InputPrimitiveTypeNumElements();
            theTemplateMap["outputType"] = OutputPrimitiveTypeString();
        }
    }
}