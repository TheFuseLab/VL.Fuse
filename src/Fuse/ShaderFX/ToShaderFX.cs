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
${sourceCompute}
        return ${result};
    }
};";

        public ToShaderFX(GpuValue<T> theCompute, string theShaderSource = ShaderSource) : base(theCompute, theShaderSource)
        {
        }
    }
}