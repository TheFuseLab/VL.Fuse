using System;
using Fuse;
using NUnit.Framework;

namespace PatchTests
{
    public class TestTypeHelpers
    {
        [Test]
        public static void TestBooleanOperator()
        {
            var gpuValue0 = new ValueInput<float>();
            
            var toFloat4 = new ToFloat4(gpuValue0);
            Console.WriteLine(toFloat4.BuildSourceCode());
        }
    }
}