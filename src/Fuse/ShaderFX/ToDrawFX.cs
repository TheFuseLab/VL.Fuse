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

    stage override void VSMain()
    {
${sourceVS}
    }
    stage override void PSMain()
    {
${sourcePS}
    }
};";
        
        
        public ToDrawFX(
            ShaderNode<GpuVoid> theVertexNode,
            ShaderNode<GpuVoid> thePixelNode, 
            List<string> theDefinedStreams = null, 
            string theTemplate = ShaderSource
            ) : base( 
            new Dictionary<string, AbstractShaderNode>
            {
                {"VS", theVertexNode},
                {"PS", thePixelNode}
            }, 
            GetDefinedStreams(theDefinedStreams),
            new Dictionary<string, string>(),
            false,
            theTemplate)
        {
        }

        public override void AppendInputs(Dictionary<string, string> theTemplateMap)
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