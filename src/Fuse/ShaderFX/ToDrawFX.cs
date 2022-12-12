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
        
        private static readonly List<string> DefinedStreams = new List<string>()
        {
            "ShadingPosition",
            
            "Color",
            
            "ColorTarget",
            "ColorTarget1",
            "ColorTarget2",
            "ColorTarget3",
            "ColorTarget4",
            "ColorTarget5",
            "ColorTarget6",
            "ColorTarget7",
            
            "TexCoord",
            "TexCoord1",
            "TexCoord2",
            "TexCoord3",
            "TexCoord4",
            "TexCoord5",
            "TexCoord6",
            "TexCoord7",
            "TexCoord8",
            "TexCoord9",
        };

        private const string ShaderSource = @"shader ${shaderID} : VS_PS_Base, ColorBase, Texturing${mixins}
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
            List<string> theDefinedStreams = null, 
            string theTemplate = ShaderSource
            ) : base( 
            new List<AbstractStage>()
            {
                new VertexStage(theVertexNode),
                theGeometryStage,
                new PixelStage(thePixelNode)
            }, 
            GetDefinedStreams(theDefinedStreams),
            new Dictionary<string, string>(),
            false,
            theTemplate)
        {
        }
        
        private static List<string> GetDefinedStreams(List<string> theDefinedStreams)
        {
            if (theDefinedStreams == null) return DefinedStreams;
            var result = new List<string>();
            result.AddRange(theDefinedStreams);
            result.AddRange(DefinedStreams);
            return result;
        }
    }
}