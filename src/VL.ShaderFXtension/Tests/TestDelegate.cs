using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace VL.ShaderFXtension.Tests
{
    public class TestDelegate
    {
        public static void TestDelegateFunction()
        {
            var delegate0 = new DelegateParameter<float>("x", new ConstantValue<float>(0));
            var delegate1 = new DelegateParameter<float>("y", new ConstantValue<float>(0));
            
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();
            
            var sin2 = new IntrinsicFunctionNode<float>(
                new OrderedDictionary<string, AbstractGpuValue> {{"val1", delegate0.Output}},
                "sin", new ConstantValue<float>(0));
            var operatorNode = new OperatorNode<float, float>(new List<GpuValue<float>> {sin2.Output, delegate1.Output},new ConstantValue<float>(0),"+");
            
            var delegateNode = new DelegateNode<float>(operatorNode.Output, new OrderedDictionary<string, AbstractGpuValue> {{"x",gpuValue0.Output}, {"y",gpuValue1.Output}}, false);
            
            Console.WriteLine(operatorNode.BuildSourceCode());
            Console.WriteLine(delegateNode.BuildFunctions());
            Console.WriteLine(delegateNode.BuildSourceCode());
            
            var delegateNodeTemplate = new DelegateNode<float>(operatorNode.Output, new OrderedDictionary<string, AbstractGpuValue> {{"x",delegate0.Output}, {"y",delegate1.Output}}, true);
            
            Console.WriteLine(delegateNodeTemplate.BuildSourceCode());
            Console.WriteLine(delegateNodeTemplate.BuildFunctions());
            Console.WriteLine(delegateNodeTemplate.BuildSourceCode());
        }

        public static void TestTemplateDelegateFunction()
        {
            var offset = new DelegateParameter<Vector2>("offset", new ConstantValue<Vector2>(0));
            var cellDistanceIns = new OrderedDictionary<string, AbstractGpuValue>
            {
                {"offset", offset.Output}
            };

            var cellDistanceCode = @"float get(float2 offset)
        {
            return  sqrt(dot( offset, offset ));
        }";
            
            var cellDistance = new CustomFunctionNode<float>(cellDistanceIns, "cellDistance",cellDistanceCode, new ConstantValue<float>(0), new List<string>(){"x"}, new List<string>(), null);
            
            var x = new GPUInput<Vector2>();
            var inputs = new OrderedDictionary<string, AbstractGpuValue>
            {
                {"x", x.Output},
                {"cellDistance", cellDistance.Output}
            };
            var worleyCode = @"
float ${signature}(float2 p)
{
    float2 n = floor( p );
    float2 f = frac( p );

    float f1 = 8.0;
    float f2 = 8.0;
    
    
    for( int j=-1; j<=1; j++ )
    for( int i=-1; i<=1; i++ )
    {
        float2 g = float2(i,j);
        float2 o = hash22( n + g );
        float2 r = g - f + o;

        float d = ${cellDistance}(r);

        if( d<f1 ) 
        { 
            f2 = f1; 
            f1 = d; 
        }
        else if( d<f2 ) 
        {
            f2 = d;
        }
    }
    
    return ${cellFunction}(float2(f1, f2));
}";
            var customFunction = new CustomFunctionNode<float>(inputs, "worley",worleyCode, new ConstantValue<float>(0), new List<string>(){"x"}, new List<string>(), null);
            Console.WriteLine(customFunction.BuildFunctions());
            Console.WriteLine(customFunction.BuildSourceCode());
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
            Console.WriteLine(delegateNode.BuildSourceCode());
            
            var delegate0 = new DelegateParameter<float>("x", new ConstantValue<float>(0));
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
            Console.WriteLine(delegateNodeTemplate.BuildSourceCode());
        }
    }
}