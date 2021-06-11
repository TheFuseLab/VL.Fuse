using System.Collections.Generic;
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
${sourceCS}
    }
};";

        public ToComputeFx(GpuValue<GpuVoid> theCompute) : base(
            new Dictionary<string, IDictionary<string, AbstractGpuValue>> {
                {
                    "CS", new Dictionary<string, AbstractGpuValue>{{"val1", theCompute}}
                }
            }, 
            new Dictionary<string, AbstractGpuValue>(),
            new List<string>(),
            new Dictionary<string, string>(),
            ComputeShaderSource)
        {
        }
    }
}