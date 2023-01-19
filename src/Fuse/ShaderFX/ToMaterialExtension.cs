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
    
    public class GenerateNormal_VSStage : AbstractStage
    {
        private const string ShaderSource = @"
	stage override void GenerateNormal_VS() 
	{
        ${sourceGenerateNormal_VS}
	}";
        
        public GenerateNormal_VSStage(ShaderNode<GpuVoid> theShaderNode) : base("GenerateNormal_VS", theShaderNode) { }

        public override string Source()
        {
            return ShaderSource;
        }
    }
    
    public class PreTransformPositionStage : AbstractStage
    {
        private const string ShaderSource = @"
	stage override void PreTransformPosition() 
	{
        ${sourcePreTransformPosition}
	}";
        
        public PreTransformPositionStage(ShaderNode<GpuVoid> theShaderNode) : base("PreTransformPosition", theShaderNode) { }

        public override string Source()
        {
            return ShaderSource;
        }
    }
    
    public class TransformPositionStage : AbstractStage
    {
        private const string ShaderSource = @"
	stage override void TransformPosition()
	{
        ${sourceTransformPosition}
	}";
        
        public TransformPositionStage(ShaderNode<GpuVoid> theShaderNode) : base("TransformPosition", theShaderNode) { }

        public override string Source()
        {
            return ShaderSource;
        }
    }
    
    public class PostTransformPositionStage : AbstractStage
    {
        private const string ShaderSource = @"
	stage override void PostTransformPosition()
	{
        ${sourcePostTransformPosition}
	}";
        
        public PostTransformPositionStage(ShaderNode<GpuVoid> theShaderNode) : base("PostTransformPosition", theShaderNode) { }

        public override string Source()
        {
            return ShaderSource;
        }
    }
    
    public class ShadingStage : AbstractStage
    {
        private const string ShaderSource = @"
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

${stageGenerateNormal_VS}

${stagePreTransformPosition}

${stageTransformPosition}

${stagePostTransformPosition}

${stageGS}

${stageShading}

};";


        public ToMaterialExtension(
            ShaderNode<GpuVoid> theGenerateNormal_VS,
            ShaderNode<GpuVoid> thePreTransform,
            ShaderNode<GpuVoid> theTransform,
            ShaderNode<GpuVoid> thePostTransform,
            GeometryStage theGeometryStage,
            ShaderNode<Vector4> theShading
        ) : base(
            new List<AbstractStage>
            {
                theGenerateNormal_VS == null ? null : new GenerateNormal_VSStage(theGenerateNormal_VS),
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