using System.Collections.Generic;
using Fuse.compute;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{
    public class ComputeStage<T> : AbstractStage
    {
        private const string ShaderSource = @"
    override void Compute()
    {
${sourceFX}
    }";
        public ComputeStage(ShaderNode<T> theShaderNode) : base("FX", theShaderNode)
        {
        }

        public override string Source()
        {
            return ShaderSource;
        }
    }
    
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

${stageFX}
};";

        public ToComputeFx(ShaderNode<T> theCompute) : base(
            new List<AbstractStage>{new ComputeStage<T>(theCompute)},
            new Dictionary<string, string>(),
            true,
            ShaderSource)
        {
        }
    }
}