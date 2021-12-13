using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse
{
    public class TextureNode<T> : ShaderNode<T>
    {

        private readonly GpuValue<Texture> _texture;
        public TextureNode(
            GpuValue<Texture> theTexture, 
            IEnumerable<AbstractGpuValue> theArguments, 
            string theFunction, 
            ConstantValue<T> theDefault
        ) : base( "textureNode", theDefault)
        {
            _texture = theTexture;
            Setup(theArguments, new Dictionary<string, string> {{"function", theFunction}});
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${texture}.${function}(${arguments});", 
                new Dictionary<string, string>
                {
                    {"texture", _texture.ID}
                });
        }
    }
}