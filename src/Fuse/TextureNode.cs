using System.Collections.Generic;
using System.Linq;
using Stride.Graphics;

namespace Fuse
{
    public class TextureNode<T> : ShaderNode<T>
    {

        private readonly GpuValue<Texture> _texture;
        private readonly List<AbstractGpuValue> _arguments;
        public TextureNode(
            GpuValue<Texture> theTexture, 
            IEnumerable<AbstractGpuValue> theArguments, 
            string theFunction, 
            ConstantValue<T> theDefault
        ) : base( "textureNode", theDefault)
        {
            _texture = theTexture;
            _arguments = theArguments.ToList();

            var ins = new List<AbstractGpuValue>(_arguments) {theTexture};
            Setup(ins, new Dictionary<string, string> {{"function", theFunction}});
        }
        
        protected override string SourceTemplate()
        {
            if (ShaderNodesUtil.HasNullValue(_arguments))
            {
                return GenerateDefaultSource();
            }
            
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${texture}.${function}(${arguments});", 
                new Dictionary<string, string>
                {
                    {"texture", _texture.ID}
                });
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            var result =  new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType", Output != null ? Output.TypeName() : TypeHelpers.GetGpuTypeForType<T>()},
                {"default", Default == null ? "": Default.ID},
                {"arguments", ShaderNodesUtil.BuildArguments(_arguments)}
            };
            if (_texture != null) result["texture"] = _texture.ID;

            return result;
        }
    }
}