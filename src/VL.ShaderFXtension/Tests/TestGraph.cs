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
                "sin", new ConstantValue<float>(0));
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
                new List<string> {"Voronoise", "Hash"}, 
                new ConstantValue<Vector2>(0)
            );
            Console.WriteLine(customFunction.BuildMixIns());
            Console.WriteLine(customFunction.SourceCode());
            Console.WriteLine(customFunction.References.Count);
        }

        public static void TestOperatorNode()
        {
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();
            var operatorNode = new OperatorNode<float, float>(new List<GpuValue<float>> {gpuValue0.Output, gpuValue1.Output},new ConstantValue<float>(0),"+");
            var operatorNodeWithNull = new OperatorNode<float, float>(new List<GpuValue<float>> {gpuValue0.Output, gpuValue1.Output, null},new ConstantValue<float>(0),"+");
            
            Console.WriteLine(operatorNode.SourceCode());
            Console.WriteLine(operatorNodeWithNull.SourceCode());
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
                "sin", new ConstantValue<float>(0));
            
            var customFunction = new FunctionNode<Vector2>(
                new OrderedDictionary<string, AbstractGpuValue>
                {
                    {"x", new ConstantValue<Vector2>(0.5f)}, 
                    {"u", gpuValue0.Output},
                    {"v", gpuValue0.Output}
                },
                "voronoise2D",
                new List<string> {"Voronoise", "Hash"},
                new ConstantValue<Vector2>(0),
                new OrderedDictionary<string, string>(){{"bla", "${resultType} bla"}}
            );
            Console.WriteLine(customFunction.BuildFunctions());
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
                "sin", new ConstantValue<float>(0));
            Console.WriteLine(sin2.SourceCode());
            Console.WriteLine(sin2.BuildFunctions());
            Console.WriteLine(sin2.BuildMixIns());
        }

        public static void TestCustomFunctions()
        {
            
            var Base = new GPUInput<Vector3>();
            var Blend = new GPUInput<Vector3>();
            var Opacity = new GPUInput<Vector3>();
            
            var inputs = new OrderedDictionary<string, AbstractGpuValue>
            {
                {"Base", Base.Output},
                {"Blend", Blend.Output},
                {"Opacity", Opacity.Output},
            };
            var template = @"
        ${resultType} ${signature}(${resultType} Base, ${resultType} Blend, float Opacity)
        {
            ${resultType} result1 = 1.0 - 2.0 * (1.0 - Base) * (1.0 - Blend);
            ${resultType} result2 = 2.0 * Base * Blend;
            ${resultType} zeroOrOne = step(Blend, 0.5);
            ${resultType} Out = result2 * zeroOrOne + (1 - zeroOrOne) * result1;
            return lerp(Base, Out, Opacity);
        }
";
            
            var customFunction = new CustomFunctionNode<Vector4>(inputs, "blendModeHardLight", template, new ConstantValue<Vector4>(0), new List<string>());
            
            Console.WriteLine(customFunction.SourceCode());
            Console.WriteLine(customFunction.BuildFunctions());
        }

        public static void TestFloatJoins()
        {
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();
            var gpuValue2 = new GPUInput<float>();
            var gpuValue3 = new GPUInput<float>();
            
            var join2 = new Float2Join(gpuValue0.Output, gpuValue1.Output);
            Console.WriteLine(join2.SourceCode());
            var join2Null = new Float2Join(gpuValue0.Output, null);
            Console.WriteLine(join2Null.SourceCode());
            
            var join3 = new Float3Join(gpuValue0.Output, gpuValue1.Output, gpuValue2.Output);
            Console.WriteLine(join3.SourceCode());
            var join3Null = new Float3Join(gpuValue0.Output, gpuValue1.Output, null);
            Console.WriteLine(join3Null.SourceCode());
            
            var join4 = new Float4Join(gpuValue0.Output, gpuValue1.Output, gpuValue2.Output, gpuValue3.Output);
            Console.WriteLine(join4.SourceCode());
            var join4Null = new Float4Join(gpuValue0.Output, gpuValue1.Output, gpuValue2.Output, null);
            Console.WriteLine(join4Null.SourceCode());
        }

        public static void TestMember()
        {
            var texCoord = new Semantic<Vector2>("TexCoord");
            var member = new GpuValueMember<Vector2, float>(texCoord.Output, "x");
            Console.WriteLine(member.SourceCode());
            
            var memberNull = new GpuValueMember<Vector2, float>(null, "x");
            Console.WriteLine(memberNull.SourceCode());
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
                "sin", new ConstantValue<float>(0));
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

            var customFunction = new FunctionNode<Vector2>(
                new OrderedDictionary<string, AbstractGpuValue>
                {
                    {"x", new ConstantValue<Vector2>(0.5f)}, 
                    {"u", gpuValue0.Output},
                    {"v", gpuValue0.Output}
                },
                "voronoise2D",
                new List<string> {"Voronoise", "Hash"}, new ConstantValue<Vector2>(0)
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
                "sin", new ConstantValue<float>(0));
            Console.WriteLine(sin2.SourceCode());
            Console.WriteLine(sin2.BuildFunctions());
        }
    }
}