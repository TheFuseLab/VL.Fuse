using System.Collections.Generic;
using Fuse.compute;
using Stride.Core.Mathematics;
using Stride.Engine;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{
    public class ToParticleFX : AbstractToShaderFX<GpuVoid>  , IComputeVoid
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
};

shader MyParticleProvider_ShaderFX : ComputeFloat, ParticleProvider${mixins}
{
    cbuffer PerMaterial
    {
        ${declarations}
    }

${structs}

${functions}

    rgroup PerMaterial
    {
        ${bufferDeclarations}
    }

    compose ComputeFloat Value;

    stage stream float pSize;

    override stage float4 GetWorldPosition()
    {
        ${sourceParticle}
        return float4(${resultParticle}, 1);
    }

    override stage float3 GetWorldNormal()
    {
        return normalize(streams.PositionWS.xyz);
    }

    override stage float GetParticleSize()
    {
        return streams.pSize;
    }

    override float Compute()
    {
${sourceFX}
        return ${resultFX};
    }
};";


        public ToParticleFX(Game theGame, GpuValue<Vector3> thePosition, GpuValue<float> theSize, GpuValue<float> theValue) : base(theGame,
            new Dictionary<string, IDictionary<string, AbstractGpuValue>> {
                
                {"FX", new Dictionary<string, AbstractGpuValue>
                {
                    {"val1", theValue}
                }},
                {"Particle", new Dictionary<string, AbstractGpuValue>
                    {
                        {"pPosition", thePosition},
                        {"pSize", theSize}
                    }
                }
            }, 
            new Dictionary<string, AbstractGpuValue>{{"resultParticle", thePosition}},
            new List<string>(),
            new Dictionary<string, string>(),
            ComputeShaderSource)
        {
        }
    }
}