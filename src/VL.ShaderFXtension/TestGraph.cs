using System;
using System.Collections.Generic;
using Stride.Core.Extensions;
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

            var add = new AddNode<float>(new List<GpuValue<float>>
                {gpuValue0.Output, gpuValue1.Output, gpuValue0.Output});

            var gpuValue2 = new GPUInput<float>();
            var add2 = new AddNode<float>(new List<GpuValue<float>> {add.Output, gpuValue2.Output});

            var sin = new IntrinsicFunctionNode<float>(
                new OrderedDictionary<string, AbstractGpuValue> {{"val1", add.Output}, {"val2", gpuValue2.Output}},
                "sin");
            Console.WriteLine(add2.SourceCode());
            Console.WriteLine(sin.SourceCode());
            Console.WriteLine(add2.Declarations());

            sin.Inputs().ForEach(Console.WriteLine);
            var inputs = new OrderedDictionary<string, AbstractGpuValue> {{"in", gpuValue0.Output}};
            var customExpression = new CustomExpressionNode<float>(inputs, "${resultType} ${resultName} = -1 * ${in};",
                new OrderedDictionary<string, string>());
            Console.WriteLine(customExpression.SourceCode());

            var texCoord = new Semantic<Vector2>("TexCoord");
            var add3 = new AddNode<Vector2>(new List<GpuValue<Vector2>> {texCoord.Output, texCoord.Output});
            Console.WriteLine(add3.SourceCode());

            var member = new GpuValueMember<Vector2, float>(texCoord.Output, "x");
            Console.WriteLine(member.SourceCode());

            var customFunction = new FunctionNode<Vector2>(
                new OrderedDictionary<string, AbstractGpuValue>
                {
                    {"x", new ConstantValue<Vector2>(0.5f)}, 
                    {"u", gpuValue0.Output},
                    {"v", gpuValue0.Output}
                },
                "voronoise2D",
                new List<string> {"Voronoise", "Hash"}
            );
            Console.WriteLine(customFunction.Mixins());
            Console.WriteLine(customFunction.SourceCode());
            Console.WriteLine(customFunction.References.Count);


            customFunction.References.ForEach(k => Console.WriteLine());
            var myReference = customFunction.References["x"];
            Console.WriteLine(myReference is GPUReference<Vector2>);
            var fbm = new FBMNode<Vector2, float>((GPUReference<Vector2>) myReference,
                new OrderedDictionary<string, AbstractGpuValue>
                {
                    {"x", texCoord.Output}, 
                    {"gain", new ConstantValue<float>(0.5f)}, 
                    {"octaves", new ConstantValue<float>(0.5f)}, 
                    {"lacunarity", new ConstantValue<float>(0.5f)}
                });
            Console.WriteLine(fbm.Functions);
            Console.WriteLine(fbm.SourceCode());
        }
    }
}