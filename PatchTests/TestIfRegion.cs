using System;
using System.Collections.Generic;
using Fuse;
using Fuse.compute;
using Fuse.regions;
using Fuse.ShaderFX;
using NUnit.Framework;
using Stride.Rendering.Materials;

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
            
            var toShaderFx = new ToShaderFX<GpuVoid>(assign as ShaderNode<GpuVoid>);
            var context = new ShaderGeneratorContext();
            toShaderFx.GenerateShaderSource(context, null);
            Console.WriteLine(toShaderFx.ShaderCode);
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
            var add = new OperatorNode<float, float>(sin, in0, null,"+");
       
            var outputs = new List<AbstractShaderNode> {add, in0, add};

            var crossLinks = new List<AbstractShaderNode>{add};
            var ifRegion = new IfRegionNode(
                check,
                inputs,
                outputs,
                crossLinks);
            var ins = new Dictionary<string, AbstractShaderNode> {{"val1", ifRegion.OptionalOutputs[0] as ShaderNode<float>}};
            Console.WriteLine(ins["val1"]);
            
            var add2 = new OperatorNode<float, float>(ifRegion.OptionalOutputs[0] as ShaderNode<float>, ifRegion.OptionalOutputs[1] as ShaderNode<float>, null,"+");

            var toShaderFx = new ToShaderFX<float>(add2 as ShaderNode<float>);
            var context = new ShaderGeneratorContext();
            toShaderFx.GenerateShaderSource(context, null);
            Console.WriteLine(toShaderFx.ShaderCode);
            
        }
        
        [Test]
        public void TestCrosslinkIf()
        {
            var check = new GpuInput<bool>();
            var in0 = new GpuInput<float>();
            var in1 = new GpuInput<float>();
            var inputs = new List<AbstractShaderNode> {in0};

            var outputs = new List<AbstractShaderNode> {in1};

            var crossLinks = new List<AbstractShaderNode>{in1};
            var ifRegion = new IfRegionNode(
                check,
                inputs,
                outputs,
                crossLinks);
            var ins = new Dictionary<string, AbstractShaderNode> {{"val1", ifRegion.OptionalOutputs[0] as ShaderNode<float>}};

            
            var toShaderFx = new ToShaderFX<float>(ifRegion.OptionalOutputs[0] as ShaderNode<float>);
            var context = new ShaderGeneratorContext();
            toShaderFx.GenerateShaderSource(context, null);
            Console.WriteLine(toShaderFx.ShaderCode);
            
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
            
            var ifRegion = new IfRegionNode(
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
            
            var ifRegion = new IfRegionNode(
                check,
                inputs,
                outputs,
                crossLinks);
            
            Console.WriteLine(ifRegion.BuildSourceCode());
        }
    }
}