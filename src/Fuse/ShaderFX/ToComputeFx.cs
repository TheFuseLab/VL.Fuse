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

${structs}

${functions}

${compositions}

    override void Compute()
    {
${sourceCS}
    }
};";

        public ToComputeFx(Game theGame, ShaderNode<T> theCompute) : base(theGame,
            theCompute, 
            new List<string>(),
            new Dictionary<string, string>(),
            true,
            ShaderSource)
        {
        }
    }
}