﻿using System;
using System.Collections.Generic;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;

namespace VL.ShaderFXtension
{
    public class TestGraph
    {
        public static void TestInputs()
        {
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();

            var add = new AddNode<float>(new List<GpuValue<float>>
                {gpuValue0.Output, gpuValue1.Output, gpuValue0.Output});

            var gpuValue2 = new GPUInput<float>();
            var add2 = new AddNode<float>(new List<GpuValue<float>> {add.Output, gpuValue2.Output});

            var sin = new IntrinsicFunctionNode<float>(
                new OrderedDictionary<string, AbstractGpuValue> {{"val1", add.Output}, {"val2", gpuValue2.Output}},
                "sin");
            Console.WriteLine(sin.SourceCode());

            sin.Inputs().ForEach(input => Console.WriteLine(input.ID));
        }

        public static void TestDeclarations()
        {
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();

            var add = new AddNode<float>(new List<GpuValue<float>>
                {gpuValue0.Output, gpuValue1.Output, gpuValue0.Output});

            var gpuValue2 = new GPUInput<float>();
            var add2 = new AddNode<float>(new List<GpuValue<float>> {add.Output, gpuValue2.Output});
            Console.WriteLine(add2.Declarations());
        }

        public static void TestMixins()
        {
            var gpuValue0 = new GPUInput<float>();
            Console.WriteLine(gpuValue0.Declaration);
            
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
            Console.WriteLine(customFunction.BuildMixIns());
            Console.WriteLine(customFunction.SourceCode());
            Console.WriteLine(customFunction.References.Count);
        }

        public static void TestOperatorNode()
        {
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();
            var add = new OperatorNode<float, float>(gpuValue0.Output, gpuValue1.Output,new ConstantValue<float>(0),"+");
            
            
            Console.WriteLine(add.SourceCode());
        }
        
        public static void TestHallo()
        {
            
            
            Console.WriteLine("HALLO");
        }

        public static void TestFunctions()
        {
            
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();
            var gpuValue2 = new GPUInput<float>();
            var texCoord = new Semantic<Vector2>("TexCoord");
            
            var add = new AddNode<float>(new List<GpuValue<float>>
                {gpuValue0.Output, gpuValue1.Output, gpuValue0.Output});
            
            var sin = new IntrinsicFunctionNode<float>(
                new OrderedDictionary<string, AbstractGpuValue> {{"val1", add.Output}, {"val2", gpuValue2.Output}},
                "sin");
            
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
            var myReference = customFunction.References["x"];
            var fbm = new FBMNode<Vector2, float>((GPUReference<Vector2>) myReference,
                new OrderedDictionary<string, AbstractGpuValue>
                {
                    {"x", texCoord.Output}, 
                    {"gain", new ConstantValue<float>(0.5f)}, 
                    {"octaves", new ConstantValue<float>(0.5f)}, 
                    {"lacunarity", sin.Output}
                });
            var sin2 = new IntrinsicFunctionNode<float>(
                new OrderedDictionary<string, AbstractGpuValue> {{"val1", fbm.Output}, {"val2", gpuValue2.Output}},
                "sin");
            Console.WriteLine(sin2.SourceCode());
            Console.WriteLine(sin2.BuildFunctions());
            Console.WriteLine(sin2.BuildMixIns());
        }

        public static void TestFloat4Join()
        {
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();
            var gpuValue2 = new GPUInput<float>();
            var gpuValue3 = new GPUInput<float>();
            var join = new Float4Join(gpuValue0.Output, gpuValue1.Output, gpuValue2.Output, gpuValue3.Output);
            Console.WriteLine(join.BuildFunctions());
        }
        
        public static void TestTraversal()
        {
            var texCoord = new Semantic<Vector2>("TexCoord");
            var gpuValue0 = new GPUInput<Vector2>();
            var add = new OperatorNode<Vector2, Vector2>(texCoord.Output, gpuValue0.Output,new ConstantValue<Vector2>(0),"*");

            var memberX = new GpuValueMember<Vector2, float>(add.Output,"x");
            var memberY = new GpuValueMember<Vector2, float>(add.Output,"y");

            var float2Join = new Float2Join(memberX.Output, memberY.Output);
            
            var memberX1 = new GpuValueMember<Vector2, float>(float2Join.Output,"x");
            var memberY1 = new GpuValueMember<Vector2, float>(float2Join.Output,"y");
            
            
            var add1 = new OperatorNode<float, float>(memberX.Output, memberY1.Output,new ConstantValue<float>(0),"+");
            
            Console.WriteLine(add1.SourceCode());
        }

        public static void TestAssign()
        {
            var gpuValue0 = new GPUInput<Vector2>();
            var gpuValue1 = new GPUInput<Vector2>();
            var add = new AddNode<Vector2>(new List<GpuValue<Vector2>>
                {gpuValue0.Output, gpuValue1.Output});
            var add2 = new AddNode<Vector2>(new List<GpuValue<Vector2>>
                {gpuValue0.Output, gpuValue0.Output});
            
            var assign = new AssignNode<Vector2>(add.Output, add2.Output);
            
            Console.WriteLine(assign.SourceCode());
        }

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
            Console.WriteLine(customFunction.BuildMixIns());
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
                    {"lacunarity", sin.Output}
                });
            Console.WriteLine(fbm.BuildFunctions());
            Console.WriteLine(fbm.SourceCode());
            
            var sin2 = new IntrinsicFunctionNode<float>(
                new OrderedDictionary<string, AbstractGpuValue> {{"val1", fbm.Output}, {"val2", gpuValue2.Output}},
                "sin");
            Console.WriteLine(sin2.SourceCode());
            Console.WriteLine(sin2.BuildFunctions());
        }
    }
}