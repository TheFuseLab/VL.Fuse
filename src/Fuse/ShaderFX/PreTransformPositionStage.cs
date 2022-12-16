using System.Collections.Generic;
using Fuse.compute;
using Fuse.ShaderFX;

namespace Fuse
{
    public class PreTransformPositionStage : AbstractStage
    {
        private const string ShaderSource = @"
    //override shading, create sphere impostor in this case
	stage override void PreTransformPosition()
	{
        ${sourcePTP}
	}";
        
        public PreTransformPositionStage(ShaderNode<GpuVoid> theShaderNode) : base("PTP", theShaderNode)
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
}