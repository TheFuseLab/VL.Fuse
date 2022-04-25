using System.Collections.Generic;
using System.Linq;
using Fuse.compute;
using NuGet;
using Stride.Graphics;

namespace Fuse
{
    
    public class TextureGetDimensions : ShaderNode<GpuVoid>
    {
        private readonly ShaderNode<Texture> _texture;
        private readonly ShaderNode<int> _mipLevel;

        public TextureGetDimensions(ShaderNode<Texture> theTexture, ShaderNode<int> mipLevel, int dimension):base("textureGetDimension")
        {
            _texture = theTexture;
            _mipLevel = mipLevel;
            
            OptionalOutputs = new List<AbstractShaderNode>();
            
            var myInputs = new List<AbstractShaderNode> {mipLevel};

            for (var i = 0; i < dimension + 1; i++)
            {
                var myDeclareValue = new DeclareValue<float>();
                myInputs.Add(myDeclareValue);
                OptionalOutputs.Add(myDeclareValue);
            }
            Setup(myInputs);
        }

        protected override string SourceTemplate()
        {
            var result = new Dictionary<string, string>();
            if (_texture != null) result["texture"] = _texture.ID;
            
            return ShaderNodesUtil.Evaluate("${texture}.GetDimensions(${arguments});", result);
        }
        
        protected void Setup(IEnumerable<AbstractShaderNode> theArguments, IEnumerable<InputModifier> theModifiers, string theFunction)
        {
            
        }
    }
    public class TextureNode<T> : ShaderNode<T>
    {

        private readonly ShaderNode<Texture> _texture;
        private readonly List<AbstractShaderNode> _arguments;
        private readonly string _functionName;
        public TextureNode(
            ShaderNode<Texture> theTexture, 
            IEnumerable<AbstractShaderNode> theArguments, 
            string theFunction, 
            ShaderNode<T> theDefault
        ) : base( "textureNode", theDefault)
        {
            _texture = theTexture;
            _arguments = theArguments.ToList();
            _functionName = theFunction;

            var ins = new List<AbstractShaderNode>(_arguments) {theTexture};
            Setup(ins);
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
                {"function", _functionName},
                {"resultName", ID},
                {"resultType", TypeName()},
                {"arguments", ShaderNodesUtil.BuildArguments(_arguments)}
            };
            if (_texture != null) result["texture"] = _texture.ID;
            
            result.AddRange(base.CreateTemplateMap());

            return result;
        }
    }
}