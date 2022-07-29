using System;
using System.Collections.Generic;
using Fuse;
using Fuse.compute;
using Fuse.ComputeSystem;
using Fuse.ShaderFX;
using NUnit.Framework;
using Stride.Core.Mathematics;
using Stride.Rendering.Materials;

namespace PatchTests
{
    public static class TestComputeSystem
    {
        [Test]
        public static void TestCompute01()
        {
            var position = new Attribute<Vector3>("particle", "position", true, false);
            var velocity = new Attribute<Vector3>("particle", "velocity", true, false);

            var add = new OperatorNode<Vector3, Vector3>(position, velocity, null, "+");
            var assign = new AssignNode<Vector3>(position,add);
            var computeStage = new ComputeStage(assign)
            {
                WriteAttributes = true
            };

            var resourceHandler = new BufferResourceHandler(null);

            var computeSystem = new ComputeSystem();
            computeSystem.Update(resourceHandler,new List<IComputeStage>(){computeStage });
            
            var context = new ShaderGeneratorContext();
            var source = computeStage.Node.GenerateShaderSource(context, null);
            
            
            
            Console.WriteLine(computeStage.Node.ShaderCode);
        }
        
        
    }
}