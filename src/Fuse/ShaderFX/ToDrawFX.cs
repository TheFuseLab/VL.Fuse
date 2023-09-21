using System.Collections.Generic;
using Fuse.compute;

namespace Fuse.ShaderFX
{
    public class VertexStage : AbstractStage
    {
        private const string ShaderSource = @"
    stage override void VSMain()
    {
${sourceVS}
    }";
        public VertexStage(ShaderNode<GpuVoid> theShaderNode) : base("VS", theShaderNode)
        {
        }

        public override void AppendInputs(Dictionary<string, string> theTemplateMap)
        {
          
        }

        public override string Source()
        {
            return ShaderSource;
        }
    }
    
    public class PixelStage : AbstractStage
    {
        private const string ShaderSource = @"
    stage override void PSMain()
    {
${sourcePS}
    }";
        public PixelStage(ShaderNode<GpuVoid> theShaderNode) : base("PS", theShaderNode)
        {
        }

        public override void AppendInputs(Dictionary<string, string> theTemplateMap)
        {
          
        }

        public override string Source()
        {
            return ShaderSource;
        }
    }

    public class ToDrawFX : AbstractToShaderFX<GpuVoid> 
    {
        private const string ShaderSource = @"shader ${shaderID} : ShaderBase${mixins}
{
    rgroup Inputs{
${groupDeclarations}
    }

    cbuffer Inputs{
${declarations}
    }

${constantArrays}

${structs}

${streams}

${functions}

${compositions}

${stageVS}

${stageGS}

${stagePS}

};";
        
        
        public ToDrawFX(
            ShaderNode<GpuVoid> theVertexNode,
            GeometryStage theGeometryStage,
            ShaderNode<GpuVoid> thePixelNode, 
            string theTemplate = ShaderSource
            ) : base( 
            new List<AbstractStage>()
            {
                new VertexStage(theVertexNode),
                theGeometryStage,
                new PixelStage(thePixelNode)
            },
            new Dictionary<string, string>(),
            false,
            theTemplate)
        {
        }
    }
}