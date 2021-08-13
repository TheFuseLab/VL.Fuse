using System.Collections.Generic;
using Fuse.compute;
using Stride.Core.Mathematics;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{
    public class ToComputeMatrix : AbstractToShaderFX<Matrix>  , IComputeValue<Matrix>
    {
        
        private const string ComputeShaderSource = @"
shader ${shaderID} : ComputeMatrix, ComputeShaderBase${mixins}
{

${structs}

    cbuffer PerDispatch{
${declarations}
    }

${functions}

    override float4x4 Compute()
    {
${sourceCS}
    }
};";

        public ToComputeMatrix(GpuValue<Matrix> theCompute) : base(
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