using System;
using System.Collections.Generic;

namespace VL.ShaderFXtension.Tests
{
    public class TestDelegate
    {
        public static void TestDelegateFunction()
        {
            var delegate0 = new DelegateParameter<float>("x");
            var delegate1 = new DelegateParameter<float>("y");
            
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();
            
            var sin2 = new IntrinsicFunctionNode<float>(
                new OrderedDictionary<string, AbstractGpuValue> {{"val1", delegate0.Output}},
                "sin", new ConstantValue<float>(0));
            var operatorNode = new OperatorNode<float, float>(new List<GpuValue<float>> {sin2.Output, delegate1.Output},new ConstantValue<float>(0),"+");
            
            var delegateNode = new DelegateNode<float>(operatorNode.Output, new OrderedDictionary<string, AbstractGpuValue> {{"x",gpuValue0.Output}, {"y",gpuValue1.Output}}, false);
            
            Console.WriteLine(operatorNode.SourceCode());
            Console.WriteLine(delegateNode.BuildFunctions());
            Console.WriteLine(delegateNode.SourceCode());
            
            var delegateNodeTemplate = new DelegateNode<float>(operatorNode.Output, new OrderedDictionary<string, AbstractGpuValue> {{"x",delegate0.Output}, {"y",delegate1.Output}}, true);
            
            Console.WriteLine(delegateNodeTemplate.SourceCode());
            Console.WriteLine(delegateNodeTemplate.BuildFunctions());
            Console.WriteLine(delegateNodeTemplate.SourceCode());
        }

        public static void TestDelegate1Function()
        {
            
            var gpuValue0 = new GPUInput<float>();
            var delegateNode = new Delegate1Node<float,float>(
                param =>
                {
                    return new IntrinsicFunctionNode<float>(
                        new OrderedDictionary<string, AbstractGpuValue> {{"val1", param}},
                        "sin",
                        new ConstantValue<float>(0)).Output;
                }, 
                gpuValue0.Output,
            false
            );
            
            Console.WriteLine(delegateNode.BuildFunctions());
            Console.WriteLine(delegateNode.SourceCode());
            
            var delegate0 = new DelegateParameter<float>("x");
            var delegateNodeTemplate = new Delegate1Node<float,float>(
                param =>
                {
                    return new IntrinsicFunctionNode<float>(
                        new OrderedDictionary<string, AbstractGpuValue> {{"val1", param}},
                        "sin",
                        new ConstantValue<float>(0)).Output;
                }, 
                delegate0.Output,
                true
            );
            
            Console.WriteLine(delegateNodeTemplate.BuildFunctions());
            Console.WriteLine(delegateNodeTemplate.SourceCode());
        }
    }
}