using System.Collections.Generic;
using Fuse.compute;
using Stride.Engine;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{
    public class ToComputeFx : AbstractToShaderFX<GpuVoid>  , IComputeVoid
    {
        
        private const string ComputeShaderSource = @"
shader ${shaderID} : ComputeVoid, ComputeShaderBase${mixins}
{

${structs}

    cbuffer PerDispatch{
${declarations}
    }

${functions}

    override void Compute()
    {
${sourceCS}
    }
};";

        public ToComputeFx(Game theGame, GpuValue<GpuVoid> theCompute) : base(theGame,
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