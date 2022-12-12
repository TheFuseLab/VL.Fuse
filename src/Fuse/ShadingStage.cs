using System.Collections.Generic;
using Fuse.ShaderFX;

namespace Fuse
{
    public class ShadingStage : AbstractStage
    {
        private const string ShaderSource = @"
    //override shading, create sphere impostor in this case
	stage override float4 Shading()
	{
        ${sourceShading}
		return ${resultShading};
	}";
        
        public ShadingStage(AbstractShaderNode theShaderNode) : base("Shading", theShaderNode)
        {
        }

        public override void AppendInputs(Dictionary<string, string> theTemplateMap)
        {
            theTemplateMap["resultShading"] = StageNode.ID +";";
        }

        public override string Source()
        {
            return ShaderSource;
        }
    }
}