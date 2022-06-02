using System.Collections.Generic;
using System.Text;
using Stride.Engine;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{
    public class ToShaderFX<T> : AbstractToShaderFX<T> where T : struct
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

${structs}

${functions}

${compositions}

    override ${resultType} Compute()
    {
${sourceFX}
        return ${resultFX};
    }
};";

        
        
        
        public ToShaderFX(Game theGame, ShaderNode<T> theCompute, bool theIsCompute = false, string theShaderSource = ShaderSource) : base(theGame, 
            new Dictionary<string, IDictionary<string, AbstractShaderNode>> {
                {
                    "FX", new Dictionary<string, AbstractShaderNode>{{"val1", theCompute}}
                }
            }, 
            new Dictionary<string, AbstractShaderNode>{{"FX",theCompute}},
            new List<string>(),
            new Dictionary<string, string>(),
            theIsCompute,
            theShaderSource)
        {
        }

       
    }
}