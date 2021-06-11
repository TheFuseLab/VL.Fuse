using Fuse.compute;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{
    public class ToComputeFx : AbstractToShaderFX<GpuVoid>  , IComputeVoid
    {
        
        

        private const string ComputeShaderSource = @"
shader ${shaderID} : ComputeVoid, ComputeShaderBase${mixins}
{
    cbuffer PerDispatch{
${declarations}
    }

${structs}

${functions}

    override void Compute()
    {
${sourceCompute}
    }
};";


        public ToComputeFx(GpuValue<GpuVoid> theCompute) : base(theCompute, ComputeShaderSource)
        {
        }
    }
}