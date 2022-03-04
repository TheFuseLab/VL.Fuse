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
        public void TestValueExchangeIf()
        {
            var check = new GpuInput<bool>().Output;
            var in0 = new GpuInput<float>().Output;
            var in1 = new GpuInput<float>().Output;
            var in2 = new GpuInput<float>().Output;
            var inputs = new List<AbstractGpuValue> {in0, in1, in2};

            var in3 = new GpuInput<float>().Output;
            var sin = new IntrinsicFunctionNode<float>(new List<AbstractGpuValue>{in3}, "sin", null).Output;
            var add = new OperatorNode<float, float>(sin, null, null,"+").Output;
       
            var outputs = new List<AbstractGpuValue> {in1, in0, add};

            var crossLinks = new List<AbstractGpuValue>{add};
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
            var check = new GpuInput<bool>().Output;
            
            var bufferIn = new BufferInput<float>("buffer").Output;
            var indexIn = new GpuInput<int>().Output;
            var in3 = new GpuInput<float>().Output;
            var sin = new IntrinsicFunctionNode<float>(new List<AbstractGpuValue>{in3}, "sin", null).Output;
            var add = new OperatorNode<float, float>(sin, null, null,"+").Output;

            var bufferSet = new TypedBufferSet<float>(bufferIn, indexIn, add).Output;

            var inEmpty = new EmptyVoid().Output;
            var inputs = new List<AbstractGpuValue> {inEmpty};
            
            
            var outputs = new List<AbstractGpuValue> {bufferSet};
            
            var crossLinks = new List<AbstractGpuValue> {add};
            
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
            var check = new GpuInput<bool>().Output;
            
            var bufferIn = new BufferInput<float>("buffer").Output;
            var indexIn = new GpuInput<int>().Output;
            
            var in3 = new GpuInput<float>().Output;
            var sin = new IntrinsicFunctionNode<float>(new List<AbstractGpuValue>{in3}, "sin", null).Output;
            var add = new OperatorNode<float, float>(sin, in3, null,"+").Output;

            var bufferSet = new TypedBufferSet<float>(bufferIn, indexIn, add).Output;

            var inEmpty = new EmptyVoid().Output;
            var inputs = new List<AbstractGpuValue> {inEmpty};
            
            var outputs = new List<AbstractGpuValue> {bufferSet};
            
            var crossLinks = new List<AbstractGpuValue> {bufferSet};
            
            var ifRegion = new IfRegion.IfRegionNode(
                check,
                inputs,
                outputs,
                crossLinks);
            
            Console.WriteLine(ifRegion.BuildSourceCode());
        }
    }
}