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
    
    public class PreTransformPositionStage : AbstractStage
    {
        private const string ShaderSource = @"
    //override shading, create sphere impostor in this case
	stage override void PreTransformPosition() 
	{
        ${sourcePreTP}
	}";
        
        public PreTransformPositionStage(ShaderNode<GpuVoid> theShaderNode) : base("PreTP", theShaderNode) { }

        public override string Source()
        {
            return ShaderSource;
        }
    }
    
    public class TransformPositionStage : AbstractStage
    {
        private const string ShaderSource = @"
    //override shading, create sphere impostor in this case
	stage override void TransformPosition()
	{
        ${sourceTP}
	}";
        
        public TransformPositionStage(ShaderNode<GpuVoid> theShaderNode) : base("TP", theShaderNode) { }

        public override string Source()
        {
            return ShaderSource;
        }
    }
    
    public class PostTransformPositionStage : AbstractStage
    {
        private const string ShaderSource = @"
    //override shading, create sphere impostor in this case
	stage override void PostTransformPosition()
	{
        ${sourcePostTP}
	}";
        
        public PostTransformPositionStage(ShaderNode<GpuVoid> theShaderNode) : base("PostTP", theShaderNode) { }

        public override string Source()
        {
            return ShaderSource;
        }
    }
    
    public class ShadingStage : AbstractStage
    {
        private const string ShaderSource = @"
    //override shading, create sphere impostor in this case
	stage override float4 Shading()
	{
        ${sourceShading}
		return ${resultShading};
	}";
        
        public ShadingStage(AbstractShaderNode theShaderNode) : base("Shading", theShaderNode) { }

        public override void AppendInputs(Dictionary<string, string> theTemplateMap)
        {
            theTemplateMap["resultShading"] = StageNode.ID +";";
        }

        public override string Source()
        {
            return ShaderSource;
        }
    }
    
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

${stagePreTP}

${stageTP}

${stagePostTP}

${stageGS}

${stageShading}

};";


        public ToMaterialExtension(
            ShaderNode<GpuVoid> thePreTransform,
            ShaderNode<GpuVoid> theTransform,
            ShaderNode<GpuVoid> thePostTransform,
            GeometryStage theGeometryStage,
            ShaderNode<Vector4> theShading
        ) : base(
            new List<AbstractStage>
            {
                thePreTransform == null ? null : new PreTransformPositionStage(thePreTransform),
                theTransform == null ? null : new TransformPositionStage(theTransform),
                thePostTransform == null ? null : new PostTransformPositionStage(thePostTransform),
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