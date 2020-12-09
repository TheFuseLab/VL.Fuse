using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace VL.ShaderFXtension
{
    public class TestGraph
    {
        
            public static void Main()
            {
                var gpuValue0 = new GPUInput<float>();
                Console.WriteLine(gpuValue0.Declaration);
                var gpuValue1 = new GPUInput<float>();
                Console.WriteLine(gpuValue1.Declaration);

                var add = new AddNode<float>(new List<GpuValue<float>> {gpuValue0.Output, gpuValue1.Output, gpuValue0.Output});
                
                var gpuValue2 = new GPUInput<float>();
                var add2 = new AddNode<float>(new List<GpuValue<float>> {add.Output, gpuValue2.Output});
                
                var sin = new IntrinsicFunctionNode<float>(new List<GpuValue<float>> {add.Output, gpuValue2.Output}, "sin");
                Console.WriteLine(add2.SourceCode());
                Console.WriteLine(sin.SourceCode());
                Console.WriteLine(add2.Declarations());
                
                sin.Inputs().ForEach(Console.WriteLine);
                var inputs = new Dictionary<string,AbstractGpuValue>{{"in",gpuValue0.Output}};
                var customExpression = new CustomExpressionNode<float>(inputs,"${resultType} ${resultName} = -1 * ${in};", new Dictionary<string, string>());
                Console.WriteLine(customExpression.SourceCode());
                
                var texCoord = new Semantic<Vector2>("TexCoord");
                var add3 = new AddNode<Vector2>(new List<GpuValue<Vector2>> {texCoord.Output, texCoord.Output});
                Console.WriteLine(add3.SourceCode());
                
                var member = new GpuValueMember<Vector2, float>(texCoord.Output,"x");
                Console.WriteLine(member.SourceCode());
                
                var customFunction = new CustomFunctionNode<float>(
                    new List<AbstractGpuValue> {texCoord.Output, gpuValue0.Output, gpuValue1.Output},
                    "voronoise2D", 
                    new List<string>{"Voronoise", "Hash"}
                    );
                Console.WriteLine(customFunction.Mixins());
                Console.WriteLine(customFunction.SourceCode());
            }
    }
}