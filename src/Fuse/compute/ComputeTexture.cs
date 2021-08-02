using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse.compute
{

    public class ComputeTextureGet<T> : ShaderNode<T> where T : struct
    {
        private readonly GpuValue<Texture> _texture;
        private readonly AbstractGpuValue _index;
        
        public ComputeTextureGet(GpuValue<Texture> theTexture, AbstractGpuValue theIndex, ConstantValue<T> theDefault) : base( "getTextureValue", theDefault)
        {
            _texture = theTexture;
            _index = theIndex;
            Setup(new List<AbstractGpuValue>(){theTexture,theIndex});
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
        private readonly GpuValue<Texture> _texture;
        private readonly AbstractGpuValue _index;
        private readonly GpuValue<TIn> _value;
    
        public ComputeTextureSet(GpuValue<Texture> theTexture, AbstractGpuValue theIndex, GpuValue<TIn> theValue) : base( "setTextureValue")
        {
            _texture = theTexture;
            _index = theIndex;
            _value = theValue;
            
            Setup(new List<AbstractGpuValue>(){theTexture,theIndex,theValue});
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