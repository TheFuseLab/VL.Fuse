using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Fuse
{
    public class SampleTextureNode : ShaderNode<Vector4>
    {
        public SampleTextureNode(GpuValue<Texture> theTexture, GpuValue<SamplerState> theSampler, GpuValue<Vector2> theTexCoords, ConstantValue<Vector4> theDefault) : base( "sampleTexture", theDefault)
        {
            const string shaderCode = "${resultType} ${resultName} = ${texture}.Sample(${sampler},${texCoords});";
            var inputs = new OrderedDictionary<string, AbstractGpuValue>()
            {
                {"texture", theTexture}, 
                {"sampler", theSampler}, 
                {"texCoords", theTexCoords}
            };
            
            Setup(shaderCode, inputs);
            
        }
    }
}