using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Fuse
{
    public class SampleTextureNode : ShaderNode<Vector4>
    {
        public SampleTextureNode(GpuValue<Texture> theTexture, GpuValue<SamplerState> theSampler, GpuValue<Vector2> theTexCoords, ConstantValue<Vector4> theDefault) : base( "sampleTexture", theDefault)
        {
            Setup(
                new List<AbstractGpuValue>() {theTexture, theSampler, theTexCoords},
                new Dictionary<string, string>()
                {
                    {"texture", theTexture.ID}, 
                    {"sampler", theSampler.ID}, 
                    {"texCoords", theTexCoords.ID}
                }
            );

        }

        protected override string SourceTemplate()
        {
            return "${resultType} ${resultName} = ${texture}.Sample(${sampler},${texCoords});";
        }
    }
}