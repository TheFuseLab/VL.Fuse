using System;
using System.Collections.Generic;

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

                var add = new AddNode<float>(new List<GPUValue<float>> {gpuValue0.Output, gpuValue1.Output, gpuValue0.Output});
                
                var gpuValue2 = new GPUInput<float>();
                var add2 = new AddNode<float>(new List<GPUValue<float>> {add.Output, gpuValue2.Output});
                
                var sin = new IntrinsicFunctionNode<float>(new List<GPUValue<float>> {add.Output, gpuValue2.Output}, "sin");
                Console.WriteLine(add2.SourceCode());
                Console.WriteLine(sin.SourceCode());
                Console.WriteLine(add2.Declarations());
            }
    }
}