using System;
using System.Collections.Generic;
using Fuse;
using Fuse.compute;
using Fuse.regions;
using NUnit.Framework;

namespace PatchTests
{
    public class TestIfRegion
    {
        [Test]
        public void TestAbstractPassThrough()
        {
            var in0 = new GpuInput<float>();
            var decl = new DeclareValue<float>();
            var assign = new AssignNode<float>(decl, in0);
            AbstractCreation.AbstractShaderNodePassThrough(assign);
            var pass = new PassThroughNode<GpuVoid>(assign);
        }
        
        [Test]
        public void TestValueExchangeIf()
        {
            var check = new GpuInput<bool>();
            var in0 = new GpuInput<float>();
            var in1 = new GpuInput<float>();
            var in2 = new GpuInput<float>();
            var inputs = new List<AbstractShaderNode> {in0, in1, in2};

            var in3 = new GpuInput<float>();
            var sin = new IntrinsicFunctionNode<float>(new List<AbstractShaderNode>{in3}, "sin", null);
            var add = new OperatorNode<float, float>(sin, null, null,"+");
       
            var outputs = new List<AbstractShaderNode> {in1, in0, add};

            var crossLinks = new List<AbstractShaderNode>{add};
            var ifRegion = new IfRegion.IfRegionNode(
                check,
                inputs,
                outputs,
                crossLinks);

            Console.WriteLine(ifRegion.BuildSourceCode());
        }

        [Test]
        public void TestSimpleCrossLink()
        {
            var check = new GpuInput<bool>();
            
            var bufferIn = new BufferInput<float>("buffer");
            var indexIn = new GpuInput<int>();
            var in3 = new GpuInput<float>();
            var sin = new IntrinsicFunctionNode<float>(new List<AbstractShaderNode>{in3}, "sin", null);
            var add = new OperatorNode<float, float>(sin, null, null,"+");

            var bufferSet = new TypedBufferSet<float>(bufferIn, indexIn, add);

            var inEmpty = new EmptyVoid();
            var inputs = new List<AbstractShaderNode> {inEmpty};
            
            
            var outputs = new List<AbstractShaderNode> {bufferSet};
            
            var crossLinks = new List<AbstractShaderNode> {add};
            
            var ifRegion = new IfRegion.IfRegionNode(
                check,
                inputs,
                outputs,
                crossLinks);
            
            Console.WriteLine(ifRegion.BuildSourceCode());
        }
        
        [Test]
        public void TestSimpleVoidCrossLink()
        {
            var check = new GpuInput<bool>();
            
            var bufferIn = new BufferInput<float>("buffer");
            var indexIn = new GpuInput<int>();
            
            var in3 = new GpuInput<float>();
            var sin = new IntrinsicFunctionNode<float>(new List<AbstractShaderNode>{in3}, "sin", null);
            var add = new OperatorNode<float, float>(sin, in3, null,"+");

            var bufferSet = new TypedBufferSet<float>(bufferIn, indexIn, add);

            var inEmpty = new EmptyVoid();
            var inputs = new List<AbstractShaderNode> {inEmpty};
            
            var outputs = new List<AbstractShaderNode> {bufferSet};
            
            var crossLinks = new List<AbstractShaderNode> {bufferSet};
            
            var ifRegion = new IfRegion.IfRegionNode(
                check,
                inputs,
                outputs,
                crossLinks);
            
            Console.WriteLine(ifRegion.BuildSourceCode());
        }
    }
}