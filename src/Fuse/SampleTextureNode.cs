using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Fuse
{
    public class SampleTextureNode<TTexCoord> : ShaderNode<Vector4>
    {

        private readonly GpuValue<Texture> _texture;
        private readonly GpuValue<SamplerState> _sampler;
        private readonly GpuValue<TTexCoord> _texCoord;
        private readonly GpuValue<float> _level;
        public SampleTextureNode(
            GpuValue<Texture> theTexture, 
            GpuValue<SamplerState> theSampler, 
            GpuValue<TTexCoord> theTexCoords, 
            GpuValue<float> theLevel, 
            GpuValue<Vector4> theDefault
            ) : base( "sampleTexture", theDefault)
        {
            _texture = theTexture;
            _sampler = theSampler;
            _texCoord = theTexCoords;
            _level = theLevel ?? new ConstantValue<float>(0);
            Setup(new List<AbstractGpuValue> {theTexture, theSampler, theTexCoords, theLevel});
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${texture}.SampleLevel(${sampler},${texCoords}, ${level});", 
                new Dictionary<string, string>
                {
                    {"texture", _texture.ID}, 
                    {"sampler", _sampler.ID}, 
                    {"texCoords", _texCoord.ID}, 
                    {"level", _level.ID}
                });
        }
    }

}