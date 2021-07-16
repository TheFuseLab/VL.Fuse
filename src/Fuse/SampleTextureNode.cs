using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Fuse
{
    public class SampleTextureNode : ShaderNode<Vector4>
    {

        private readonly GpuValue<Texture> _texture;
        private readonly GpuValue<SamplerState> _sampler;
        private readonly GpuValue<Vector2> _texCoord;
        private readonly GpuValue<float> _level;
        public SampleTextureNode(GpuValue<Texture> theTexture, GpuValue<SamplerState> theSampler, GpuValue<Vector2> theTexCoords, GpuValue<float> theLevel, ConstantValue<Vector4> theDefault) : base( "sampleTexture", theDefault)
        {
            _texture = theTexture;
            _sampler = theSampler;
            _texCoord = theTexCoords;
            _level = theLevel ?? new ConstantValue<float>(0);
            Setup(new List<AbstractGpuValue>() {theTexture, theSampler, theTexCoords});
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