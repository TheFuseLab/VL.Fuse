using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse.compute
{

    public class ComputeTextureGet<T> : ShaderNode<T> where T : struct
    {
        private readonly ShaderNode<Texture> _texture;
        private readonly AbstractShaderNode _index;
        
        public ComputeTextureGet(ShaderNode<Texture> theTexture, AbstractShaderNode theIndex, ShaderNode<T> theDefault) : base( "getTextureValue", theDefault)
        {
            _texture = theTexture;
            _index = theIndex;
            Setup(new List<AbstractShaderNode>(){theTexture,theIndex});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${resultType} ${resultName} = ${textureName}[${index}];";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"textureName", _texture.ID},
                {"index", _index.ID}
            });
        }
    }

    public class ComputeTextureSet<TIn> : ShaderNode<GpuVoid> where TIn : struct
    {
        private readonly ShaderNode<Texture> _texture;
        private readonly AbstractShaderNode _index;
        private readonly ShaderNode<TIn> _value;
    
        public ComputeTextureSet(ShaderNode<Texture> theTexture, AbstractShaderNode theIndex, ShaderNode<TIn> theValue) : base( "setTextureValue")
        {
            _texture = theTexture;
            _index = theIndex;
            _value = theValue;
            
            Setup(new List<AbstractShaderNode>(){theTexture,theIndex,theValue});
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${textureName}[${index}] = ${value};";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"textureName", _texture.ID},
                {"index", _index.ID},
                {"value", _value.ID}
            });
        }
    }
    
    public class ComputeTextureAbstractSet : ShaderNode<GpuVoid> 
    {
        private readonly ShaderNode<Texture> _texture;
        private readonly AbstractShaderNode _index;
        private readonly AbstractShaderNode _value;
    
        public ComputeTextureAbstractSet(ShaderNode<Texture> theTexture, AbstractShaderNode theIndex, AbstractShaderNode theValue) : base( "setTextureValue")
        {
            _texture = theTexture;
            _index = theIndex;
            _value = theValue;
            
            Setup(new List<AbstractShaderNode>(){theTexture,theIndex,theValue});
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${textureName}[${index}] = ${value};";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"textureName", _texture.ID},
                {"index", _index.ID},
                {"value", _value.ID}
            });
        }
    }
}