using System.Collections.Generic;

namespace Fuse.ShaderFX
{
    public class ShaderFXStage<T> : AbstractStage
    {
        private const string ShaderSource = @"
    override ${resultType} Compute()
    {
${sourceFX}
        ${resultFX}
    }";
        public ShaderFXStage(ShaderNode<T> theShaderNode) : base("FX", theShaderNode)
        {
        }

        public override void AppendInputs(Dictionary<string, string> theTemplateMap)
        {
            if (TypeHelpers.GetGpuType<T>().Equals("void"))
            {
                theTemplateMap["resultFX"] = "";
            }
            else
            {
                theTemplateMap["resultFX"] = "return " + StageNode.ID +";";
            }
        }

        public override string Source()
        {
            return ShaderSource;
        }
    }
    public class ToShaderFX<T> : AbstractToShaderFX<T> 
    {

        private const string ShaderSource = @"
shader ${shaderID} : VS_PS_Base, Texturing, Compute${shaderType}${mixins}
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

${stageFX}

};";

        public ToShaderFX(ShaderNode<T> theCompute, bool theIsCompute = false) : base( 
            new List<AbstractStage>{new ShaderFXStage<T>(theCompute)}, 
            new List<string>(),
            new Dictionary<string, string>(),
            theIsCompute,
            ShaderSource)
        {
        }
    }
}