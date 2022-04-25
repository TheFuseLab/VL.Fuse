using System;
using System.Collections.Generic;
using Fuse;
using Fuse.regions;
using NUnit.Framework;

namespace PatchTests
{
    public class TestFunction
    {
        [Test]
        public static void TestPatchedFunction()
        {
              
           var functionParameter0 = new FunctionParameter<float>(new ConstantValue<float>(0),0);
           var functionParameter1 = new FunctionParameter<float>(new ConstantValue<float>(0),1);
           
        
           var sin2 = new IntrinsicFunctionNode<float>(
               new List< AbstractShaderNode> {functionParameter0},
               "sin", new ConstantValue<float>(0)
               );
           var operatorNode = new OperatorNode<float, float>(
               new List<ShaderNode<float>> {sin2, functionParameter1},
               new ConstantValue<float>(0),"+"
               );
           
           
           var gpuValue0 = new GpuInput<float>();
           var gpuValue1 = new GpuInput<float>();
           
           var patchedFunctionNode = new FunctionRegion.RegionFunctionNode<float>(new List< AbstractShaderNode> {gpuValue0,gpuValue1}, operatorNode,"addSin", new ConstantValue<float>(0f));
           
           Console.WriteLine(operatorNode.BuildSourceCode());
           
           patchedFunctionNode.FunctionMap().ForEach(kv => Console.Write(kv.Value));
           
           Console.WriteLine(patchedFunctionNode.BuildSourceCode());
        }

        static void Main(string[] args)
        {
            // Display the number of command line arguments.
            Console.WriteLine(args.Length);
        }
    }
}