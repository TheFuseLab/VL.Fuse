using System.Collections.Generic;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{
    public class ToShaderFX<T> : AbstractToShaderFX<T>, IComputeValue<T> where T : struct
    {

        private const string ShaderSource = @"
shader ${shaderID} : VS_PS_Base, Compute${shaderType}${mixins}
{
    cbuffer PerMaterial{
${declarations}
    }

${structs}

${functions}

    override ${resultType} Compute()
    {
${sourceFX}
        return ${resultFX};
    }
};";

        public ToShaderFX(GpuValue<T> theCompute, string theShaderSource = ShaderSource) : base(
            new Dictionary<string, IDictionary<string, AbstractGpuValue>> {
                {
                    "FX", new Dictionary<string, AbstractGpuValue>{{"val1", theCompute}}
                }
            }, 
            new Dictionary<string, AbstractGpuValue>{{"FX",theCompute}},
            new List<string>(),
            new Dictionary<string, string>(),
            theShaderSource)
        {
        }
    }
}