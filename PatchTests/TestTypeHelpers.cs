using System;
using Fuse;
using NUnit.Framework;
using VL.Core;

namespace PatchTests
{
    public class TestTypeHelpers
    {
        private static NodeContext _context;
        
        [Test]
        public static void TestBooleanOperator()
        {
            var gpuValue0 = new ValueInput<float>(_context);
            
            var toFloat4 = new ToFloat4(_context, gpuValue0);
            Console.WriteLine(toFloat4.BuildSourceCode());
        }
    }
}