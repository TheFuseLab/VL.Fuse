using System;
using System.Collections.Generic;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;

namespace Fuse.Tests
{
    public class TestGraph
    {
        public static void TestInputs()
        {
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();

            var add = new OperatorNode<float, float>(new List<GpuValue<float>> {gpuValue0.Output, gpuValue1.Output},new ConstantValue<float>(0),"+");

            var gpuValue2 = new GPUInput<float>();
            var add2 = new OperatorNode<float, float>(new List<GpuValue<float>> {add.Output, gpuValue2.Output},new ConstantValue<float>(0),"+");

            var sin = new IntrinsicFunctionNode<float>(
                new List<AbstractGpuValue> {add.Output, gpuValue2.Output},
                "sin", new ConstantValue<float>(0));
            Console.WriteLine(sin.BuildSourceCode());

            sin.Inputs().ForEach(input => Console.WriteLine(input.ID));
        }

        public static void TestDeclarations()
        {
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();

            var add = new OperatorNode<float, float>(new List<GpuValue<float>> {gpuValue0.Output, gpuValue1.Output},new ConstantValue<float>(0),"+");

            var gpuValue2 = new GPUInput<float>();
            var add2 = new OperatorNode<float, float>(new List<GpuValue<float>> {add.Output, gpuValue2.Output},new ConstantValue<float>(0),"+");
            Console.WriteLine(add2.Declarations());
        }

        public static void TestMixins()
        {
            var gpuValue0 = new GPUInput<float>();
            Console.WriteLine(gpuValue0.Declaration);
            
            var customFunction = new MixinFunctionNode<Vector2>(
                new List<AbstractGpuValue>
                {
                    new ConstantValue<Vector2>(0.5f), 
                    gpuValue0.Output,
                    gpuValue0.Output
                },
                "voronoise2D", 
                new ConstantValue<Vector2>(0),
                "Voronoise"
            );
            Console.WriteLine(customFunction.BuildMixIns());
            Console.WriteLine(customFunction.BuildSourceCode());
        }

        public static void TestOperatorNode()
        {
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();
            var operatorNode = new OperatorNode<float, float>(new List<GpuValue<float>> {gpuValue0.Output, gpuValue1.Output},new ConstantValue<float>(0),"+");
            var operatorNodeWithNull = new OperatorNode<float, float>(new List<GpuValue<float>> {gpuValue0.Output, gpuValue1.Output, null},new ConstantValue<float>(0),"+");
            
            Console.WriteLine(operatorNode.BuildSourceCode());
            Console.WriteLine(operatorNodeWithNull.BuildSourceCode());
        }
        
        public static void TestHallo()
        {
            
            
            Console.WriteLine("HALLO");
        }

        public static void TestCustomFunctions()
        {
            
            var Base = new GPUInput<Vector3>();
            var Blend = new GPUInput<Vector3>();
            var Opacity = new GPUInput<Vector3>();
            
            var inputs = new List<AbstractGpuValue>
            {
                Base.Output,
                Blend.Output,
                Opacity.Output,
            };
            const string template = @"
        ${resultType} ${signature}(${resultType} Base, ${resultType} Blend, float Opacity)
        {
            ${resultType} result1 = 1.0 - 2.0 * (1.0 - Base) * (1.0 - Blend);
            ${resultType} result2 = 2.0 * Base * Blend;
            ${resultType} zeroOrOne = step(Blend, 0.5);
            ${resultType} Out = result2 * zeroOrOne + (1 - zeroOrOne) * result1;
            return lerp(Base, Out, Opacity);
        }
";
            
            var customFunction = new CustomFunctionNode<Vector4>(inputs, "blendModeHardLight",template, new ConstantValue<Vector4>(0));
            
            Console.WriteLine(customFunction.BuildSourceCode());
            Console.WriteLine(customFunction.BuildFunctions());
        }

        public static void TestFloatJoins()
        {
            var gpuValue0 = new GPUInput<float>();
            var gpuValue1 = new GPUInput<float>();
            var gpuValue2 = new GPUInput<float>();
            var gpuValue3 = new GPUInput<float>();
            
            var join2 = new Float2Join(gpuValue0.Output, gpuValue1.Output);
            Console.WriteLine(join2.BuildSourceCode());
            var join2Null = new Float2Join(gpuValue0.Output, null);
            Console.WriteLine(join2Null.BuildSourceCode());
            
            var join3 = new Float3Join(gpuValue0.Output, gpuValue1.Output, gpuValue2.Output);
            Console.WriteLine(join3.BuildSourceCode());
            var join3Null = new Float3Join(gpuValue0.Output, gpuValue1.Output, null);
            Console.WriteLine(join3Null.BuildSourceCode());
            
            var join4 = new Float4Join(gpuValue0.Output, gpuValue1.Output, gpuValue2.Output, gpuValue3.Output);
            Console.WriteLine(join4.BuildSourceCode());
            var join4Null = new Float4Join(gpuValue0.Output, gpuValue1.Output, gpuValue2.Output, null);
            Console.WriteLine(join4Null.BuildSourceCode());
        }

        public static void TestMember()
        {
            var texCoord = new Semantic<Vector2>("TexCoord");
            var member = new GpuValueMember<Vector2, float>(texCoord.Output, "x");
            Console.WriteLine(member.BuildSourceCode());
            
            var memberNull = new GpuValueMember<Vector2, float>(null, "x");
            Console.WriteLine(memberNull.BuildSourceCode());
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
            
            Console.WriteLine(add1.BuildSourceCode());
        }

        public static void TestAssign()
        {
            var gpuValue0 = new GPUInput<Vector2>();
            var gpuValue1 = new GPUInput<Vector2>();
            var add = new OperatorNode<Vector2, Vector2>(new List<GpuValue<Vector2>> {gpuValue0.Output, gpuValue1.Output},new ConstantValue<Vector2>(0),"+");
            var add2 = new OperatorNode<Vector2, Vector2>(new List<GpuValue<Vector2>> {gpuValue0.Output, gpuValue0.Output},new ConstantValue<Vector2>(0),"+");
            
            var assign = new AssignNode<Vector2>(add.Output, add2.Output);
            
            Console.WriteLine(assign.BuildSourceCode());
        }

        public static void Main()
        {
            var gpuValue0 = new GPUInput<float>();
            Console.WriteLine(gpuValue0.Declaration);
            var gpuValue1 = new GPUInput<float>();
            Console.WriteLine(gpuValue1.Declaration);

            var add = new OperatorNode<float, float>(new List<GpuValue<float>>
                {gpuValue0.Output, gpuValue1.Output, gpuValue0.Output},new ConstantValue<float>(0),"+");

            var gpuValue2 = new GPUInput<float>();
            var add2 = new OperatorNode<float,float>(new List<GpuValue<float>> {add.Output, gpuValue2.Output},new ConstantValue<float>(0),"+");

            var sin = new IntrinsicFunctionNode<float>(
                new List<AbstractGpuValue> {add.Output, gpuValue2.Output},
                "sin", new ConstantValue<float>(0));
            Console.WriteLine(add2.BuildSourceCode());
            Console.WriteLine(sin.BuildSourceCode());
            Console.WriteLine(add2.Declarations());

            sin.Inputs().ForEach(Console.WriteLine);

            var texCoord = new Semantic<Vector2>("TexCoord");
            var add3 = new OperatorNode<Vector2,Vector2>(new List<GpuValue<Vector2>> {texCoord.Output, texCoord.Output},new ConstantValue<Vector2>(0),"+");
            Console.WriteLine(add3.BuildSourceCode());

            var customFunction = new MixinFunctionNode<Vector2>(
                new List<AbstractGpuValue>
                {
                    new ConstantValue<Vector2>(0.5f), 
                    gpuValue0.Output,
                    gpuValue0.Output
                },
                "voronoise2D",
                new ConstantValue<Vector2>(0),
                "Voronoise"
            );
            Console.WriteLine(customFunction.BuildMixIns());
            Console.WriteLine(customFunction.BuildSourceCode());
            
            
        }
    }
}