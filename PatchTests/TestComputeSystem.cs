using System;
using System.Collections.Generic;
using Fuse;
using Fuse.ComputeSystem;
using NUnit.Framework;
using Stride.Core.Mathematics;
using Stride.Rendering.Materials;
using VL.Core;

namespace PatchTests
{
    public static class TestComputeSystem
    {
        private static NodeContext _context;

        [Test]
        public static void TestCompute01()
        {
            var position = new Attribute<Vector3>(_context,"particle", "position", true, false);
            var velocity = new Attribute<Vector3>(_context,"particle", "velocity", true, false);

            var add = new Operator<Vector3, Vector3>(_context,null, "+");
            add.SetInput(new List<ShaderNode<Vector3>>{position, velocity});
            var assign = new AssignValue<Vector3>(_context);
            assign.SetInputs(position, velocity);
            var computeStage = new ComputeStage(_context)
            {
                WriteAttributes = true
            };
            computeStage.SetComputeInput(assign);
            var resourceHandler = new BufferResourceHandler(_context,null);

            var computeSystem = new ComputeSystem(_context);
            computeSystem.SetInput(resourceHandler, new List<AbstractComputeStage>{computeStage });
            
            var context = new ShaderGeneratorContext();
            var source = computeStage.GenerateShaderSource(context, null);
            
            
            
            Console.WriteLine(computeStage.ShaderCode);
        }
        
        
    }
}