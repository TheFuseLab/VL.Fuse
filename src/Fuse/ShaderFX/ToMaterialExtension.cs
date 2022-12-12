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

${stageGS}

${stageShading}

};";


        public ToMaterialExtension(
            GeometryStage theGeometryStage,
            ShaderNode<Vector4> theShading
        ) : base(
            new List<AbstractStage>
            {
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
    
    public class AppendStreams : SimpleKeyword
    {
    
        public AppendStreams(NodeContext nodeContext) : base( nodeContext, "outputStream.Append(streams);")
        {
        }
    }
    
    public class RestartStrip : SimpleKeyword
    {
    
        public RestartStrip(NodeContext nodeContext) : base( nodeContext, "outputStream.RestartStrip();")
        {
        }
    }
    
    public class SetStreams : ShaderNode<GpuVoid> , IComputeVoid
    {
        private ShaderNode<int> _index;
        
        public SetStreams(NodeContext nodeContext) : base( nodeContext, "getStream")
        {
            
        }

        public void SetInputs(ShaderNode<int> theIndex)
        {
            _index = theIndex;
            
            SetInputs(new List<AbstractShaderNode>{theIndex});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "streams = input[${index}];";
            
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"index", _index.ID}
            });
        }
    }
}