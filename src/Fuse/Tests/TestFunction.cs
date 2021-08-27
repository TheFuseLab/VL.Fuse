using System;
using System.Collections.Generic;
using Stride.Core.Extensions;

namespace Fuse.Tests
{
    public class TestFunction
    {
        public static void TestPatchedFunction()
        {
            var functionParameter0 = new FunctionParameter<float>(new ConstantValue<float>(0),0);
            var functionParameter1 = new FunctionParameter<float>(new ConstantValue<float>(0),1);
            
            
            var sin2 = new IntrinsicFunctionNode<float>(
                new List< AbstractGpuValue> {functionParameter0.Output},
                "sin", new ConstantValue<float>(0)
                );
            var operatorNode = new OperatorNode<float, float>(
                new List<GpuValue<float>> {sin2.Output, functionParameter1.Output},
                new ConstantValue<float>(0),"+"
                );
            
            
            var gpuValue0 = new GpuInput<float>();
            var gpuValue1 = new GpuInput<float>();
            
            var patchedFunctionNode = new PatchedFunctionNode<float>(new List< AbstractGpuValue> {gpuValue0.Output,gpuValue1.Output}, operatorNode.Output,"addSin", new ConstantValue<float>(0f));
            
            Console.WriteLine(operatorNode.BuildSourceCode());
            
            patchedFunctionNode.FunctionMap().ForEach(kv => Console.Write(kv.Value));
            
            Console.WriteLine(patchedFunctionNode.BuildSourceCode());
        }
    }
}