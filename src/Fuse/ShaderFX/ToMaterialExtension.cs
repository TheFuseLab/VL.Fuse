using System;
using System.Collections.Generic;
using System.Text;
using Fuse.compute;
using Stride.Core.Mathematics;
using Stride.Engine;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;
using Vortice.Vulkan;

namespace Fuse.ShaderFX
{
    
    
    public class ToMaterialExtension : AbstractToShaderFX<GpuVoid> 
    {

        private const string ShaderSource = @"
shader ${shaderID} : MaterialExtension${mixins}
{
    rgroup PerMaterial{
${groupDeclarations}
    }

    cbuffer PerMaterial{
${declarations}
    }

${constantArrays}

${structs}

${streams}

${functions}

${compositions}

${stagePTP}

${stageGS}

${stageShading}

};";


        public ToMaterialExtension(
            ShaderNode<GpuVoid> thePreTransform,
            GeometryStage theGeometryStage,
            ShaderNode<Vector4> theShading
        ) : base(
            new List<AbstractStage>
            {
                thePreTransform == null ? null : new PreTransformPositionStage(thePreTransform),
                theGeometryStage,
                theShading == null ? null : new ShadingStage(theShading)
            },
            new List<string>(),
            new Dictionary<string, string>(),
            false,
            ShaderSource)
        {
        }

    }
}