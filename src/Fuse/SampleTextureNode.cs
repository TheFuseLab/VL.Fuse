using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Fuse
{
    public class SampleTextureNode<TTexCoord> : ShaderNode<Vector4>
    {

        private readonly ShaderNode<Texture> _texture;
        private readonly ShaderNode<SamplerState> _sampler;
        private readonly ShaderNode<TTexCoord> _texCoord;
        private readonly ShaderNode<float> _level;
        public SampleTextureNode(
            ShaderNode<Texture> theTexture, 
            ShaderNode<SamplerState> theSampler, 
            ShaderNode<TTexCoord> theTexCoords, 
            ShaderNode<float> theLevel, 
            ShaderNode<Vector4> theDefault
            ) : base( "sampleTexture", theDefault)
        {
            _texture = theTexture;
            _sampler = theSampler;
            _texCoord = theTexCoords;
            _level = theLevel ?? new ConstantValue<float>(0);
            SetInputs(new List<AbstractShaderNode> {theTexture, theSampler, theTexCoords, theLevel});
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