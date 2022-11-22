using System.Collections.Generic;
using Fuse.compute;
using Stride.Engine;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{
    public class ToComputeFx<T> : AbstractToShaderFX<T>, IComputeVoid
    {
        
        private const string ShaderSource = @"
shader ${shaderID} : ComputeVoid, ComputeShaderBase${mixins}
{

    rgroup ResourceGroup{
${groupDeclarations}
    }

    cbuffer PerDispatch{
${declarations}
    }

${constantArrays}

${structs}

${functions}

${compositions}

    override void Compute()
    {
${sourceFX}
    }
};";

        public ToComputeFx(ShaderNode<T> theCompute) : base(
            theCompute, 
            new List<string>(),
            new Dictionary<string, string>(),
            true,
            ShaderSource)
        {
        }
    }
}